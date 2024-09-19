namespace HapetFrontend.Ast.Statements
{
	public class AstReturnStmt : AstStatement
	{
		/// <summary>
		/// Return expression (the expression after 'return' word)
		/// </summary>
		public AstExpression ReturnExpression { get; set; }

		public AstReturnStmt(AstExpression expr, ILocation Location = null) : base(Location)
		{
			ReturnExpression = expr;
		}
	}
}
