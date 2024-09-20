namespace HapetFrontend.Ast.Expressions
{
	// also used for dereference
	public class AstPointerExpr : AstExpression
	{
		/// <summary>
		/// The expression on which the pointer is applied
		/// </summary>
		public AstExpression SubExpression { get; set; }

		/// <summary>
		/// The '*' could also be used for dereferece variables and other shite. 
		/// So you should check how was the pointer applied - to a type or to a name of smth?
		/// Was it on the right side or the left side?
		/// </summary>
		public bool IsDereference { get; set; } = false;

		public AstPointerExpr(AstExpression sub, ILocation Location, bool isDeref = false)
			: base(Location)
		{
			IsDereference = isDeref;
			SubExpression = sub;
		}
	}
}
