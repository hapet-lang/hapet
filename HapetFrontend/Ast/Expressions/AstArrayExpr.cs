using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
	public class AstArrayExpr : AstExpression
	{
		public AstNestedExpr TypeName { get; set; }
		public AstExpression SizeExpr { get; set; }
		public List<AstExpression> Elements { get; set; }

		[DebuggerStepThrough]
		public AstArrayExpr(AstNestedExpr typeName, AstExpression sizeExpr, List<AstExpression> elements, ILocation Location = null) : base(Location)
		{
			TypeName = typeName;
			SizeExpr = sizeExpr;
			Elements = elements;
		}
	}
}
