using HapetFrontend;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using HapetPostPrepare.Other;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private readonly Compiler _compiler;

        /// <summary>
        /// File that is currently preparing
        /// </summary>
        public ProgramFile _currentSourceFile;

        /// <summary>
        /// To handle current stack manager
        /// </summary>
        public ParentStackManager _currentParentStack;

        /// <summary>
        /// The block that is currently preparing
        /// </summary>
        private AstBlockExpr _currentBlock;

        public PostPrepare(Compiler compiler)
        {
            _compiler = compiler;
        }

        public int StartPreparation(bool createMetadataFile = true)
        {
            _currentParentStack = ParentStackManager.Create(_compiler.MessageHandler);

            // we are checking for errors after each function 
            // because some steps won't work properly without previous
            // 0 is returned because normal error is going to be
            // returned in the caller shite

            // replace tuples before inferencing!!!
            ReplaceAllTuplesInDecls();

            PostPrepareSpecialKeys();
            if (_compiler.MessageHandler.HasErrors)
                return 0;
            PostPrepareClassMethods();
            if (_compiler.MessageHandler.HasErrors)
                return 0;
            PostPrepareScoping();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            // generate metadata file
            int result = PostPrepareMetadata(createMetadataFile);
            if (result != 0)
                return result;

            PostPrepareTypeInference();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            PostPrepareInheritedShite();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            SearchForMainFunction();
            if (_compiler.MessageHandler.HasErrors)
                return 0;
            CallAllStaticCtors();

            MakeOtherShite();
            return 0;
        }
    }
}
