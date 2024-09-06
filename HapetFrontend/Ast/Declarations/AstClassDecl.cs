using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
	public class AstClassDecl : AstDeclaration
	{
		/// <summary>
		/// Declarations that are in the class
		/// </summary>
		public List<AstDeclaration> Declarations { get; } = new List<AstDeclaration>();

		// TODO: do i need it?
		// public Scope SubScope { get; set; }

		public AstClassDecl(AstIdExpr name, List<AstDeclaration> declarations, string doc = "", ILocation Location = null) : base(name, doc, Location) 
		{
			Type = new AstIdExpr("class", Location);
			Type.OutType = new ClassType(this);

			Declarations = declarations;
		}
	}
}
