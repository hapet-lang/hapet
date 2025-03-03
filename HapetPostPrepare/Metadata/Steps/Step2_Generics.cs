using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataGenerics(AstStatement stmt)
        {
            if (stmt is AstClassDecl cls)
            {
                foreach (var t in cls.GenericNames)
                {
                    // getting constains for the generic type
                    List<AstNestedExpr> constrains = cls.GenericConstrains.TryGetValue(t, out var val) ? val : new List<AstNestedExpr>();

                    // we need to create a temp class declaration 
                    // and define it inside class scope
                    CreateTypeDeclarationForGeneric(cls, t, constrains);
                }
            }
        }
    }
}
