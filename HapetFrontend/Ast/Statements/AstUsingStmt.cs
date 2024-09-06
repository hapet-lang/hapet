using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
	public class AstUsingStmt : AstStatement
	{
		/// <summary>
		/// If the using is attached
		/// </summary>
		public bool IsAttached { get; set; }

		/// <summary>
		/// If using with 'as' word and a name that is in AsWhat
		/// </summary>
		public AstIdExpr AsWhat { get; set; }

		/// <summary>
		/// The module to be imported
		/// </summary>
		public AstIdExpr Module { get; set; }

		public AstUsingStmt(AstIdExpr module, bool isAttached = false, AstIdExpr asWhat = null, ILocation Location = null) : base(Location)
		{
			Module = module;
			IsAttached = isAttached;
			AsWhat = asWhat;
		}
	}
}
