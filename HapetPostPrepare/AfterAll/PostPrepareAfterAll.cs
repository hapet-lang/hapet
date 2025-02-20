using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using HapetFrontend;
using HapetPostPrepare.Entities;

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
            _allUsedClassesInProgram.Add(_compiler.MainFunction.ContainingParent as AstClassDecl);
            // WARN! ToList is required! because _allUsedClassesInProgram is going to be modified below for no reason
            var unique = _allUsedClassesInProgram.Distinct().ToList();
            foreach (var cls in unique)
            {
                // skip interfaces
                if (cls.IsInterface)
                    continue;

                // check that the class has suppress stor call attr
                // and skip the class without calling it's stor
                string suppressAttrName = "System.SuppressStaticCtorCallAttribute";
                var suppressAttr = cls.Attributes.FirstOrDefault(x => x.AttributeName.TryFlatten(_compiler.MessageHandler, _currentSourceFile) == suppressAttrName);
                if (suppressAttr != null)
                    continue;

                // creating stor call ast
                string funcName = $"{cls.Name.Name.Split('.').Last()}_stor";
                var call = new AstCallExpr(new AstNestedExpr(cls.Name.GetCopy(), null), new AstIdExpr(funcName));
                SetScopeAndParent(call, _compiler.MainFunction.Body, _compiler.MainFunction.Body.SubScope);
                PostPrepareExprScoping(call);
                PostPrepareExprInference(call, inInfo, ref outInfo);

                // TODO: sort the static ctors calls by hierarchy
                _compiler.MainFunction.Body.Statements.Insert(0, call);
            }
        }
    }
}
