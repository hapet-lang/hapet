using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Types;

namespace HapetFrontend.Scoping
{
    public interface ISymbol
    {
        AstIdExpr Name { get; }
    }

    /// <summary>
    /// To search for a namespace in a global scope
    /// </summary>
    public class NamespaceSymbol : ISymbol
    {
        public AstIdExpr Name { get; private set; }
        public Scope Scope { get; private set; }

        public NamespaceSymbol(AstIdExpr name, Scope scope)
        {
            this.Name = name;
            this.Scope = scope;
        }
    }

    /// <summary>
    /// To search for a declaration in a global scope. Use it for local vars, params and other
    /// </summary>
    public class DeclSymbol : ISymbol
    {
        public AstIdExpr Name { get; private set; }
        public AstDeclaration Decl { get; private set; }

        public DeclSymbol(AstIdExpr name, AstDeclaration decl)
        {
            this.Name = name;
            this.Decl = decl;
        }
    }
}
