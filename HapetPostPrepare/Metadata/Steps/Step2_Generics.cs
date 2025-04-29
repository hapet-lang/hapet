using HapetFrontend.Ast;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private bool PostPrepareMetadataGenerics(AstStatement stmt)
        {
            if (stmt is not AstDeclaration decl)
                return false;

            // we need only generics
            if (!decl.HasGenericTypes)
                return false;

            // TODO: inference constains

            // add here
            _allPureGenericTypes.Add(decl);
            return true;
        }
    }
}
