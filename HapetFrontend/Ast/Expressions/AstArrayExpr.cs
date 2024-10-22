namespace HapetFrontend.Ast.Expressions
{
	public class AstArrayExpr : AstExpression
	{
		/// <summary>
		/// The expression on which the array is applied
		/// </summary>
		public AstExpression SubExpression { get; set; }

		public AstArrayExpr(AstExpression sub, ILocation Location = null) : base(Location)
		{
			SubExpression = sub;
		}
	}
}
