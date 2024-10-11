using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
	public class AstUsingStmt : AstStatement
	{
		/// <summary>
		/// The module to be imported. Could be <see cref="AstNestedExpr"/>
		/// </summary>
		public AstNestedExpr Module { get; set; }

		public AstUsingStmt(AstNestedExpr module, ILocation Location = null) : base(Location)
		{
			Module = module;
		}
	}
}
