using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;

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

            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            // getting pure generics from decl
            var pureGenerics = GenericsHelper.GetGenericsFromName(decl.Name as AstIdGenericExpr, _compiler.MessageHandler);
            // we need to set types to ids
            for (int i = 0; i < pureGenerics.Count; ++i)
            {
                var originalGeneric = (decl.Name as AstIdGenericExpr).GenericRealTypes[i];
                var currGeneric = pureGenerics[i];
                var currContrains = new List<AstConstrainStmt>();
                if (decl.GenericConstrains.TryGetValue(currGeneric, out var constrains))
                    currContrains = constrains;

                // creating the declaration
                var genericDecl = new AstGenericDecl(currGeneric, location: currGeneric.Location)
                {
                    Constrains = currContrains,
                    ParentDecl = decl.IsImplOfGeneric ? decl.OriginalGenericDecl : decl,
                    IsNestedDecl = true, // for what? but let it be :)
                    SubScope = new Scope($"{currGeneric.Name}_scope", decl.Scope),
                    SourceFile = decl.SourceFile,
                };

                // post prepare
                PostPrepareGenericDeclConstrains(genericDecl, inInfo, ref outInfo);
                // add it to preparation pipe
                AllGenericsMetadata.Add(genericDecl);

                originalGeneric.OutType = genericDecl.Type.OutType;
                currGeneric.OutType = originalGeneric.OutType;
            }

            // add here
            _allPureGenericTypes.Add(decl);
            return true;
        }
    }
}
