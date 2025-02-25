using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;

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
            cls.SpecialKeys.Add(TokenType.KwPrivate);
            cls.Attributes.Add(new AstAttributeStmt(new AstNestedExpr(new AstIdExpr("System.SuppressStaticCtorCallAttribute", name), null, name), [], name));

            PostPrepareClassScoping(cls);
            SetScopeAndParent(cls, parent, parent.SubScope);
            parent.SubScope.DefineDeclSymbol(name.Name, cls);
            return cls;
        }
    }
}
