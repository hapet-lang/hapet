using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
	public class AstArrayExpr : AstExpression
	{
		public AstExpression TypeName { get; set; }
		public AstExpression SizeExpr { get; set; }
		public List<AstExpression> Elements { get; set; }

		[DebuggerStepThrough]
		public AstArrayExpr(AstExpression type, AstExpression sizeExpr, List<AstExpression> elements, ILocation Location = null) : base(Location)
		{
			TypeName = type;
			SizeExpr = sizeExpr;
			Elements = elements;
		}
	}
}
