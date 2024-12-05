using HapetFrontend.Scoping;

namespace HapetFrontend.Ast.Expressions
{
    public class AstBlockExpr : AstStatement
    {
        /// <summary>
        /// The statements that are in the block
        /// </summary>
        public List<AstStatement> Statements { get; set; }

		/// <summary>
		/// The inner scope of the block. Used to get access to it's content
		/// </summary>
		public Scope SubScope { get; set; }

		public AstBlockExpr(List<AstStatement> statements, ILocation Location = null) : base(Location: Location)
        {
            Statements = statements;
        }
    }
}
