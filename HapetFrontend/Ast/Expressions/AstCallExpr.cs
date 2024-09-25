namespace HapetFrontend.Ast.Expressions
{
	public class AstCallExpr : AstExpression
	{
		/// <summary>
		/// The type (for static funcs) or object (for non static) where the func is located
		/// </summary>
		public AstNestedIdExpr TypeOrObjectName { get; set; }

		/// <summary>
		/// If the call is of static func
		/// </summary>
		public bool StaticCall { get; set; }

		/// <summary>
		/// The func name
		/// </summary>
		public AstIdExpr FuncName { get; set; }

		/// <summary>
		/// The arguments to be passed into func
		/// </summary>
		public List<AstArgumentExpr> Arguments { get; set; }

		public AstCallExpr(AstNestedIdExpr typeOrObjectName, AstIdExpr funcName, List<AstArgumentExpr> arguments = null, ILocation Location = null)
			: base(Location)
		{
			this.TypeOrObjectName = typeOrObjectName;
			this.FuncName = funcName;
			this.Arguments = arguments;
		}
	}
}
