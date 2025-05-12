using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
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

            // we need only PURE generics
            if (decl.IsImplOfGeneric)
                return false;

            // getting pure generics from decl
            var pureGenerics = GenericsHelper.GetGenericsFromName(decl.Name as AstIdGenericExpr, _compiler.MessageHandler);
            // we need to set types to ids
            for (int i = 0; i < pureGenerics.Count; ++i)
            {
                var originalGeneric = (decl.Name as AstIdGenericExpr).GenericRealTypes[i];
                var currGeneric = pureGenerics[i];
                var currContrains = new List<AstNestedExpr>();
                if (decl.GenericConstrains.TryGetValue(currGeneric, out var constrains))
                    currContrains = constrains;

                // TODO: inference constains

                originalGeneric.OutType = new GenericType(currGeneric, currContrains)
                {
                    ParentDeclaration = decl.IsImplOfGeneric ? decl.OriginalGenericDecl : decl,
                };
                currGeneric.OutType = originalGeneric.OutType;
            }

            // add here
            _allPureGenericTypes.Add(decl);
            return true;
        }
    }
}
