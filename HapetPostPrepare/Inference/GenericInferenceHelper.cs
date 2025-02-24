using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private AstDeclaration GetTypeDeclarationForGeneric(AstDeclaration parent, AstIdExpr name, List<AstNestedExpr> constrains)
        {
            // TODO: handle constains
            var cls = new AstClassDecl(name, new List<AstDeclaration>(), "", name)
            {
                IsGenericType = true,
            };

            PostPrepareClassScoping(cls);
            SetScopeAndParent(cls, parent, parent.SubScope);
            parent.SubScope.DefineDeclSymbol(name.Name, cls);
            return cls;
        }
    }
}
