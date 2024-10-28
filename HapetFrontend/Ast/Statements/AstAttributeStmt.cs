using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
	public class AstAttributeStmt : AstStatement
	{
		/// <summary>
		/// The name of the attribute
		/// </summary>
		public AstNestedExpr AttributeName { get; set; }

		/// <summary>
		/// Parameters of the attribute
		/// </summary>
		public List<AstExpression> Parameters { get; set; }

		public AstAttributeStmt(AstNestedExpr attrName, List<AstExpression> parameters, ILocation Location = null) : base(Location)
		{
			AttributeName = attrName;
			Parameters = parameters;
		}
	}
}
