using HapetFrontend;
using HapetPostPrepare;

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

            HandleLambdas();

            CreateRequired();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            ReplaceAllProperties();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            CheckUsedDecls();

            ReplaceAllClasses();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            AppendVirtuals();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            return 0;
        }
    }
}
