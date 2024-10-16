using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
	public class AstArrayAccessExpr : AstExpression
	{
		public AstNestedExpr ObjectName { get; set; }
		/// <summary>
		/// It could be not only an Int. but also a String (for dicts) and other shite
		/// </summary>
		public AstExpression ParameterExpr { get; set; }

		[DebuggerStepThrough]
		public AstArrayAccessExpr(AstNestedExpr objectName, AstExpression parameterExpr, ILocation Location = null) : base(Location)
		{
			ObjectName = objectName;
			ParameterExpr = parameterExpr;
		}
	}
}
