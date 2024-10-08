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
		public AstNestedExpr AsWhat { get; set; }

		/// <summary>
		/// The module to be imported. Could be <see cref="AstNestedExpr"/>
		/// </summary>
		public AstNestedExpr Module { get; set; }

		public AstUsingStmt(AstNestedExpr module, bool isAttached = false, AstNestedExpr asWhat = null, ILocation Location = null) : base(Location)
		{
			Module = module;
			IsAttached = isAttached;
			AsWhat = asWhat;
		}
	}
}
