using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Types;
using HapetFrontend;
using HapetPostPrepare.Entities;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Helpers;
using HapetFrontend.Ast;
using HapetFrontend.Enums;
using HapetFrontend.Parsing;
using System.Drawing;
using System.Runtime;
using HapetFrontend.Scoping;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void SearchForMainFunction()
        {
            foreach (var clsDecl in _serializeClassesMetadata)
            {
                foreach (var decl in clsDecl.Declarations)
                {
                    if (decl is not AstFuncDecl)
                        continue;

                    var funcDecl = decl as AstFuncDecl;
                    if (funcDecl.Name.Name == "Main" &&
                        funcDecl.Returns.OutType == HapetType.CurrentTypeContext.GetIntType(4, true) &&
                        funcDecl.Parameters.Count == 1)
                    {
                        _compiler.MainFunction = funcDecl;
                    }
                }
            }

            // check for main func existance if required
            if (_compiler.MainFunction == null && (_compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Console || _compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Windowed))
            {
                _compiler.MessageHandler.ReportMessage([], ErrorCode.Get(CTEN.NoMainFunction));
            }
        }

        private void CallAllStaticCtors()
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            // call other stor callers in caller and all current stors
            _compiler.StorsCallerFunction = CreateStorsCallerFunc();
            AstBlockExpr bodyOfStorsToCall = _compiler.StorsCallerFunction.Body;

            // we also need to call dependent projects' stor_callers
            foreach (var d in _compiler.CurrentProjectData.AllReferencedProjectNames)
            {
                // creating stor call ast
                string funcName = $"{d}_stor_caller";
                var call = new AstCallExpr(null, new AstIdExpr(funcName));
                call.IsSpecialExternalCall = true;
                bodyOfStorsToCall.Statements.Insert(0, call);
            }

            // add all classes and structs that not imported
            var unique = new List<AstDeclaration>();
            unique.AddRange(AllClassesMetadata.Where(x => !x.IsImported));
            unique.AddRange(AllStructsMetadata.Where(x => !x.IsImported));

            List<AstCallExpr> allStorCalls = new List<AstCallExpr>();
            foreach (var decl in unique)
            {
                // setting current source file
                _currentSourceFile = decl.SourceFile;

                // skip interfaces
                if (decl is AstClassDecl clsDecl && clsDecl.IsInterface)
                    continue;

                // skip generic implemented types
                if (decl.IsImplOfGeneric)
                    continue;

                // check that the class has suppress stor call attr
                // and skip the class without calling it's stor
                var suppressAttr = decl.GetAttribute("System.SuppressStaticCtorCallAttribute");
                if (suppressAttr != null)
                    continue;

                // we put stor call of nested decl into its parent decl stor
                AstBlockExpr blockWhereToCall;
                if (decl.IsNestedDecl)
                {
                    var candidate = GetFuncFromCandidates(new AstIdExpr($"{decl.ParentDecl.Name.Name.GetClassNameWithoutNamespace()}_stor"), 
                        [], decl.ParentDecl, false, out var _);
                    if (candidate == null)
                    {
                        // error here 
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl, [], ErrorCode.Get(CTEN.ParentStorNotFound));
                        continue;
                    }
                    var body = (candidate.Decl as AstFuncDecl).Body;
                    blockWhereToCall = (body.Statements[0] as AstIfStmt).BodyTrue;
                }
                else
                    blockWhereToCall = bodyOfStorsToCall;

                _currentParentStack.AddParent(decl);

                // creating stor call ast
                string funcName = $"{decl.Name.Name.GetClassNameWithoutNamespace()}_stor";
                var call = new AstCallExpr(new AstNestedExpr(decl.Name.GetCopy(), null), new AstIdExpr(funcName));
                SetScopeAndParent(call, blockWhereToCall, blockWhereToCall.SubScope);
                PostPrepareExprScoping(call);
                PostPrepareExprInference(call, inInfo, ref outInfo);

                _currentParentStack.RemoveParent();

                // if nested - add to parent stor - else sort
                if (decl.IsNestedDecl)
                    blockWhereToCall.Statements.Add(call);
                else
                    allStorCalls.Add(call);
            }
            bodyOfStorsToCall.Statements.AddRange(allStorCalls.OrderBy(StorSorterFunc));
        }

        private AstFuncDecl CreateStorsCallerFunc()
        {
            // just handlers
            Location loc = new Location(new TokenLocation(), new TokenLocation());

            List<AstStatement> storBlockStatements = new List<AstStatement>();
            var storBlock = new AstBlockExpr(storBlockStatements, loc);

            // the ctor func
            var storDecl = new AstFuncDecl(new List<AstParamDecl>(),
            new AstNestedExpr(new AstIdExpr("void", loc), null, loc),
            storBlock,
            new AstIdExpr($"{_compiler.CurrentProjectSettings.ProjectName}_stor_caller"),
            "", loc);
            storDecl.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwPublic, loc.Beginning)); // stor is public
            storDecl.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwStatic, loc.Beginning)); // stor is static
            storDecl.ClassFunctionType = ClassFunctionType.StorCaller;

            // no need for stack trace inside it
            storDecl.Attributes.Add(new AstAttributeStmt(new AstNestedExpr(new AstIdExpr("System.SuppressStackTraceAttribute"), null), [], loc));

            SetScopeAndParent(storDecl, null, _compiler.GlobalScope);
            PostPrepareDeclScoping(storDecl);
            PostPrepareStatementUpToCurrentStep(storDecl);

            return storDecl;
        }

        private int StorSorterFunc(AstCallExpr call)
        {
            //if (call.FuncName.Name.Contains("Gc"))
            //    return -1;
            //if (call.FuncName.Name.Contains("StackTrace"))
            //    return 0;
            //if (call.FuncName.Name.Contains("ExceptionHelper"))
            //    return 1;
            //else
            //    return int.MaxValue;

            // no need now. all special stors are called manually before main
            return 0;
        }

        private void MakeOtherShite()
        {
            // need to add SuppressStackTrace attr to _ini if parent has it
            // all classes and structs that not imported
            var unique = new List<AstDeclaration>();
            unique.AddRange(AllClassesMetadata.Where(x => !x.IsImported));
            unique.AddRange(AllStructsMetadata.Where(x => !x.IsImported));
            foreach (var decl in unique)
            {
                // setting current source file
                _currentSourceFile = decl.SourceFile;
                var suppressAttr = decl.GetAttribute("System.SuppressStackTraceAttribute");
                if (suppressAttr == null)
                    continue;
                string funcName = $"{decl.Name.Name.GetClassNameWithoutNamespace()}_ini";
                var iniDecl = decl.SubScope.GetSymbol(new AstIdExpr(funcName)) as DeclSymbol;
                if (iniDecl == null)
                    continue;
                iniDecl.Decl.Attributes.Add(suppressAttr);
            }
        }
    }
}
