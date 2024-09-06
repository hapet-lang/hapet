using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
	public class AstBoolExpr : AstExpression
	{
		public bool BoolValue => (bool)OutValue;

		[DebuggerStepThrough]
		public AstBoolExpr(bool value, ILocation Location = null) : base(Location)
		{
			this.OutValue = value;
		}
	}
}
