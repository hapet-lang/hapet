using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Types;

namespace HapetFrontend.Parsing.PostPrepare
{
    public partial class PostPrepare
    {
        private readonly Compiler _compiler;

        /// <summary>
        /// File that is currently preparing
        /// </summary>
        private ProgramFile _currentSourceFile;

        /// <summary>
        /// The class decl that is currently preparing
        /// </summary>
        private AstClassDecl _currentClass;

        /// <summary>
        /// The function decl that is currently preparing
        /// </summary>
        private AstFuncDecl _currentFunction;

        public PostPrepare(Compiler compiler)
        {
            _compiler = compiler;
        }

        public int StartPreparation()
        {
            // we are checking for errors after each function 
            // because some steps won't work properly without previous
            // 0 is returned because normal error is going to be
            // returned in the caller shite

            PostPrepareClassMethods();
            if (_compiler.MessageHandler.HasErrors)
                return 0;
            PostPrepareScoping();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            // generate metadata file
            int result = PostPrepareMetadata();
            if (result != 0)
                return result;

            PostPrepareTypeInference();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            SearchForMainFunction();
            if (_compiler.MessageHandler.HasErrors)
                return 0;
            CallAllStaticCtors();
            return 0;
        }

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
            if (_compiler.MainFunction == null)
                return;

            // also add main's func class
            _allUsedClassesInProgram.Add(_compiler.MainFunction.ContainingClass);
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
                PostPrepareExprInference(call);

                // TODO: sort the static ctors calls by hierarchy
                _compiler.MainFunction.Body.Statements.Insert(0, call);
            }
        }
    }
}
