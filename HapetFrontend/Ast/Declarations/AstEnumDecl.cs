using HapetFrontend.Ast.Expressions;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
	public class AstEnumDecl : AstDeclaration
	{
		/// <summary>
		/// Declarations that are in the enum (their type should be inherited from the enum type)
		/// </summary>
		public List<AstVarDecl> Declarations { get; } = new List<AstVarDecl>();

		/// <summary>
		/// The inner scope of the enum. Used to get access to it's content
		/// </summary>
		public Scope SubScope { get; set; }

		public AstEnumDecl(AstIdExpr name, List<AstVarDecl> declarations, string doc = "", ILocation Location = null) : base(name, doc, Location)
		{
			Type = new AstIdExpr("enum", Location);
			Type.OutType = new EnumType(this);

			Declarations = declarations;
		}
	}
}
