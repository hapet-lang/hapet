using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    // should be removed from file before pp and code gen!!!
    public class AstNamespaceStmt : AstStatement
    {
        /// <summary>
        /// Namespace name (the name after 'namespace' word)
        /// </summary>
        public AstNestedExpr NameExpression { get; set; }

        public AstNamespaceStmt(AstNestedExpr name, ILocation Location = null) : base(Location)
        {
            NameExpression = name;
        }
    }
}
