using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
	/// <summary>
	/// Variable (or something other) assignment ast
	/// Operators like '+=' and other are prepared on parsing step so there is only '=' operator
	/// </summary>
	public class AstAssignStmt : AstStatement
	{
		public AstNestedExpr Target { get; set; }
		public AstExpression Value { get; set; }

		public AstAssignStmt(AstNestedExpr target, AstExpression value, ILocation Location = null)
			: base(Location: Location)
		{
			this.Target = target;
			this.Value = value;
		}
	}
}
