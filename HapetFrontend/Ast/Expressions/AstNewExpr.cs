using System.Text;

namespace HapetFrontend.Ast.Expressions
{
	public class AstNewExpr : AstExpression
	{
		/// <summary>
		/// The type that has to be created
		/// </summary>
		public AstNestedExpr TypeName { get; set; }

		/// <summary>
		/// The arguments to be passed into type constructor
		/// </summary>
		public List<AstArgumentExpr> Arguments { get; set; }

		public AstNewExpr(AstNestedExpr typeName, List<AstArgumentExpr> arguments = null, ILocation Location = null)
			: base(Location)
		{
			this.TypeName = typeName;
			this.Arguments = arguments;
		}
	}
}
