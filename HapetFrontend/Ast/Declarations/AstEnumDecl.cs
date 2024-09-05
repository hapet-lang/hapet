using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
	public class AstEnumDecl : AstDeclaration
	{
		/// <summary>
		/// Declarations that are in the enum (their type should be inherited from the enum type)
		/// </summary>
		public List<AstDeclaration> Declarations { get; } = new List<AstDeclaration>();

		// TODO: do i need it?
		// public Scope SubScope { get; set; }

		public AstEnumDecl(AstIdExpr name, string doc = "", ILocation Location = null) : base(name, doc, Location)
		{
			Type = new AstIdExpr("enum", Location);
			Type.OutType = new EnumType(this);
		}
	}
}
