using HapetFrontend.Entities;
using HapetFrontend;
using HapetPostPrepare;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        private readonly Compiler _compiler;
        private readonly PostPrepare _postPreparer;

        public LastPrepare(Compiler compiler, PostPrepare postPreparer)
        {
            _compiler = compiler;
            _postPreparer = postPreparer;
        }

        public int StartPreparation()
        {
            // we are checking for errors after each function 
            // because some steps won't work properly without previous
            // 0 is returned because normal error is going to be
            // returned in the caller shite

            CreateRequired();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            ReplaceAllProperties();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            ReplaceAllClasses();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            return 0;
        }
    }
}
