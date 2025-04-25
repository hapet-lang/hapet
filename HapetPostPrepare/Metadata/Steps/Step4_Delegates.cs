using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Types;
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
                // have to do it here!!!
                del.Type.OutType = DelegateType.GetDelegateType(del, del.Scope);

                PostPrepareDelegateInference(del, inInfo, ref outInfo);
            }
        }
    }
}
