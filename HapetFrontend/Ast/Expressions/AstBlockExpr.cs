namespace HapetFrontend.Ast.Expressions
{
    public class AstBlockExpr : AstStatement
    {
        /// <summary>
        /// The statements that are in the block
        /// </summary>
        public List<AstStatement> Statements { get; set; }

        public AstBlockExpr(List<AstStatement> statements, ILocation Location = null) : base(Location: Location)
        {
            Statements = statements;
        }
    }
}
