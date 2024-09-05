using HapetFrontend.Scoping;

namespace HapetFrontend.Ast.Statements
{
	public class AstBlockStmt : AstStatement
	{
		/// <summary>
		/// The scope of the block
		/// </summary>
		public Scope Scope { get; set; }

		/// <summary>
		/// The statements that are in the block
		/// </summary>
		public List<AstStatement> Statements { get; set; }

		public AstBlockStmt(List<AstStatement> statements, ILocation Location = null) : base(Location: Location)
		{
			this.Statements = statements;
		}
	}
}
