using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

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

            // we need to set types to ids
            for (int i = 0; i < decl.GenericNames.Count; ++i)
            {
                var currGeneric = decl.GenericNames[i];
                var currContrains = decl.GenericConstrains[currGeneric];
                // TODO: inference constains
                currGeneric.OutType = new GenericType(currGeneric, currContrains);
            }

            // add here
            _allPureGenericTypes.Add(decl);
            return true;
        }
    }
}
