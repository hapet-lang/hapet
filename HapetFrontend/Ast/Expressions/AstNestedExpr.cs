namespace HapetFrontend.Ast.Expressions
{
	public class AstNestedExpr : AstExpression
	{
		/// <summary>
		/// This is the left part of an id expr like the 'a.mm.anime.Test'
		/// where 'Test' would be the <see cref="RightPart"/> and 'a.mm.anime' would be the <see cref="LeftPart"/> 
		/// with its parsed names
		/// </summary>
		public AstNestedExpr LeftPart { get; set; }

		/// <summary>
		/// The right part of the expression
		/// Could only be <see cref="AstCallExpr"/> or <see cref="AstIdExpr"/> or <see cref="AstPointerExpr"/> or real pure <see cref="AstExpression"/>
		/// </summary>
		public AstExpression RightPart { get; set; }

		public AstNestedExpr(AstExpression rightPart, AstNestedExpr leftPart, ILocation Location = null) : base(Location)
		{
			this.RightPart = rightPart;
			this.LeftPart = leftPart;
		}
	}
}
