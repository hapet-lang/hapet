using HapetFrontend.Ast.Expressions;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
	public class AstStructDecl : AstDeclaration
	{
		/// <summary>
		/// Declarations that are in the struct
		/// </summary>
		public List<AstDeclaration> Declarations { get; } = new List<AstDeclaration>();

		/// <summary>
		/// The inner scope of the struct. Used to get access to it's content
		/// </summary>
		public Scope SubScope { get; set; }

		public AstStructDecl(AstIdExpr name, string doc = "", ILocation Location = null) : base(name, doc, Location)
		{
			Type = new AstIdExpr("struct", Location);
			Type.OutType = new StructType(this);
		}
	}
}
