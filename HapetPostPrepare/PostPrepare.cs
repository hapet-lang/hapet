using HapetFrontend;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Types;

namespace HapetPostPrepare
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

            PostPrepareSpecialKeys();
            if (_compiler.MessageHandler.HasErrors)
                return 0;
            PostPrepareClassMethods();
            if (_compiler.MessageHandler.HasErrors)
                return 0;
            PostPrepareScoping();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            // replace tuples before inferencing!!!
            ReplaceAllTuplesInDecls();

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
    }
}
