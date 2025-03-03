using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetPostPrepare.Entities;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataDelegates(AstStatement stmt)
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            if (stmt is AstDelegateDecl del)
            {
                PostPrepareDelegateInference(del, inInfo, ref outInfo);
            }
        }
    }
}
