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

            AstBlockExpr bodyOfStorsToCall;
            if (_compiler.MainFunction == null)
            {
                var storCaller = CreateStorsCallerFunc();
                bodyOfStorsToCall = storCaller.Body;
            }
            else
                bodyOfStorsToCall = _compiler.MainFunction.Body;

            // add all classes and structs that not imported
            var unique = new List<AstDeclaration>();
            unique.AddRange(AllClassesMetadata.Where(x => !x.IsImported));
            unique.AddRange(AllStructsMetadata.Where(x => !x.IsImported));

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
                string suppressAttrName = "System.SuppressStaticCtorCallAttribute";
                var suppressAttr = decl.Attributes.FirstOrDefault(x => x.AttributeName.OutType is ClassType clsT && clsT.Declaration.Name.Name == suppressAttrName);
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

                // set that stor and stor_var are used
                {
                    var stor = decl.GetDeclarations().FirstOrDefault(x => x is AstFuncDecl fnc && fnc.ClassFunctionType == ClassFunctionType.StaticCtor);
                    stor.IsDeclarationUsed = true;
                    var stor_var = decl.GetDeclarations().FirstOrDefault(x => x is AstVarDecl vd && vd.IsStaticCtorField);
                    stor_var.IsDeclarationUsed = true;
                }

                _currentParentStack.RemoveParent();

                // TODO: sort the static ctors calls by hierarchy
                blockWhereToCall.Statements.Insert(0, call);
            }

            // we also need to call dependent projects' stor_callers
            foreach (var d in _compiler.CurrentProjectData.AllReferencedProjectNames)
            {
                // creating stor call ast
                string funcName = $"{d}_stor_caller";
                var call = new AstCallExpr(null, new AstIdExpr(funcName));
                call.IsSpecialExternalCall = true;
                bodyOfStorsToCall.Statements.Insert(0, call);
            }
        }

        private AstFuncDecl CreateStorsCallerFunc()
        {
            // just handlers
            Location loc = new Location(new TokenLocation(), new TokenLocation());

            List<AstStatement> storBlockStatements = new List<AstStatement>();
            var storBlock = new AstBlockExpr(storBlockStatements, loc);
            storBlock.Statements.Add(new AstReturnStmt(null));

            // the ctor func
            var storDecl = new AstFuncDecl(new List<AstParamDecl>(),
            new AstNestedExpr(new AstIdExpr("void", loc), null, loc),
            storBlock,
            new AstIdExpr($"{_compiler.CurrentProjectSettings.ProjectName}_stor_caller"),
            "", loc);
            storDecl.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwPublic, loc.Beginning)); // stor is public
            storDecl.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwStatic, loc.Beginning)); // stor is static
            storDecl.ClassFunctionType = ClassFunctionType.StorCaller;

            SetScopeAndParent(storDecl, null, _compiler.GlobalScope);
            PostPrepareDeclScoping(storDecl);
            PostPrepareStatementUpToCurrentStep(storDecl);

            return storDecl;
        }
    }
}
