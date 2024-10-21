using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
	public class AstSwitchStmt : AstStatement
	{
		/// <summary>
		/// The expression that is going to be matched
		/// </summary>
		public AstExpression SubExpression { get; set; }

		/// <summary>
		/// The cases that are in the switch
		/// </summary>
		public List<AstCaseStmt> Cases { get; set; }

		public AstSwitchStmt(AstExpression sub, List<AstCaseStmt> cases, ILocation location = null) : base(location)
		{
			SubExpression = sub;
			Cases = cases;
		}
	}

	public class AstCaseStmt : AstStatement
	{
		/// <summary>
		/// A const value that should match the switch' SubExpression
		/// </summary>
		public AstExpression Pattern { get; set; }

		/// <summary>
		/// The body of the 'case'
		/// </summary>
		public AstBlockExpr Body { get; set; }

		/// <summary>
		/// 'true' if the case is default case
		/// </summary>
		public bool DefaultCase { get; set; }

		public AstCaseStmt(AstExpression pattern, AstBlockExpr body, ILocation location = null) : base(location)
		{
			Pattern = pattern;
			Body = body;
		}
	}
}
