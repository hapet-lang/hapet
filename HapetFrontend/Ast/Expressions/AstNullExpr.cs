using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
	public class AstNullExpr : AstExpression
	{
		[DebuggerStepThrough]
		public AstNullExpr(ILocation Location = null) : base(Location)
		{
			// TODO: do i need to set here outtype to ptr?
		}
	}
}
