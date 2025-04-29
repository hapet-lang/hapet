using HapetFrontend.Ast;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private List<AstDeclaration> _allPureGenericTypes { get; } = new List<AstDeclaration>();

        public void PostPrepareGenericType(AstStatement decl)
        {
            // TODO: post prepare all insides
            // check that all callings on generic types are normal
            // and some shite like a + b allowed and etc.
        }
    }
}
