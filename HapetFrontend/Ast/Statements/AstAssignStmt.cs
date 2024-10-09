using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
	/// <summary>
	/// Variable (or something other) assignment ast
	/// </summary>
	public class AstAssignStmt : AstStatement
	{
		public AstNestedExpr Target { get; set; }
		public AstExpression Value { get; set; }
		public string Operator { get; set; }

		public AstAssignStmt(AstNestedExpr target, AstExpression value, string op = null, ILocation Location = null)
			: base(Location: Location)
		{
			this.Target = target;
			this.Value = value;
			this.Operator = op;
		}
	}
}
