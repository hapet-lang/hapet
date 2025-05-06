using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Types;
using HapetFrontend;
using HapetPostPrepare.Entities;
using HapetFrontend.Ast.Statements;

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
                    if (funcDecl.Name.Name.EndsWith("Main(System.String[])") &&
                        funcDecl.Returns.OutType == IntType.GetIntType(4, true) &&
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

            if (_compiler.MainFunction == null)
                return;

            // also add main's func class
            _allUsedClassesAndStructsInProgram.Add(_compiler.MainFunction.ContainingParent as AstClassDecl);
            // WARN! ToList is required! because _allUsedClassesInProgram is going to be modified below for no reason
            var unique = _allUsedClassesAndStructsInProgram.Distinct().ToList();
            foreach (var decl in unique)
            {
                // setting current source file
                _currentSourceFile = decl.SourceFile;

                // skip interfaces
                if (decl is AstClassDecl clsDecl && clsDecl.IsInterface)
                    continue;

                // skip generic (non-real) classes
                if (decl.HasGenericTypes)
                    continue;

                // check that the class has suppress stor call attr
                // and skip the class without calling it's stor
                string suppressAttrName = "System.SuppressStaticCtorCallAttribute";
                var suppressAttr = decl.Attributes.FirstOrDefault(x => x.AttributeName.TryFlatten(_compiler.MessageHandler, _currentSourceFile) == suppressAttrName);
                if (suppressAttr != null)
                    continue;

                // we put stor call of nested decl into its parent decl stor
                AstBlockExpr blockWhereToCall;
                if (decl.IsNestedDecl)
                {
                    var candidate = GetFuncFromCandidates(new AstIdExpr($"{decl.ParentDecl.Name.Name.GetClassNameWithoutNamespace()}_stor"), 
                        null, [], decl.ParentDecl, out var _);
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
                    blockWhereToCall = _compiler.MainFunction.Body;

                // creating stor call ast
                string funcName = $"{decl.Name.Name.GetClassNameWithoutNamespace()}_stor";
                var call = new AstCallExpr(new AstNestedExpr(decl.Name.GetCopy(), null), new AstIdExpr(funcName));
                SetScopeAndParent(call, blockWhereToCall, blockWhereToCall.SubScope);
                PostPrepareExprScoping(call);
                PostPrepareExprInference(call, inInfo, ref outInfo);

                // TODO: sort the static ctors calls by hierarchy
                blockWhereToCall.Statements.Insert(0, call);
            }
        }
    }
}
