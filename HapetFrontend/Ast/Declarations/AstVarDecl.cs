using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
	public class AstVarDecl : AstDeclaration
	{
		/// <summary>
		/// A value to init the var
		/// </summary>
		public AstExpression Initializer { get; set; }

		/// <summary>
		/// The class/struct/interface that contains the var
		/// Used only for fields and properties!!!
		/// </summary>
		public AstDeclaration ContainingParent { get; set; }

		public AstVarDecl(AstExpression type, AstIdExpr name, AstExpression ini = null, string doc = "", ILocation Location = null) : base(name, doc, Location)
		{
			Type = type;
			Initializer = ini;
		}
	}
}
