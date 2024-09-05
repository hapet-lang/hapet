using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Declarations
{
	public class AstParamDecl : AstDeclaration
	{
		public AstExpression DefaultValue { get; set; }

		/// <summary>
		/// The function in which the parameter presented
		/// </summary>
		public AstFuncDecl ContainingFunction { get; set; }

		public AstParamDecl(AstIdExpr type, AstIdExpr name, AstExpression defaultValue = null, string doc = "", ILocation Location = null) : base(name, doc, Location)
		{
			Type = type;
			DefaultValue = defaultValue;
		}
	}
}
