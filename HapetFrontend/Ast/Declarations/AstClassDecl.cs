using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
	public class AstClassDecl : AstDeclaration
	{
		/// <summary>
		/// Declarations that are in the class
		/// </summary>
		public List<AstDeclaration> Declarations { get; } = new List<AstDeclaration>();

		/// <summary>
		/// The list of types from which the current class is inherited
		/// </summary>
		public List<AstNestedExpr> InheritedFrom { get; set; } = new List<AstNestedExpr>();

		/// <summary>
		/// The inner scope of the class. Used to get access to it's content
		/// </summary>
		public Scope SubScope { get; set; }

		public AstClassDecl(AstIdExpr name, List<AstDeclaration> declarations, string doc = "", ILocation Location = null) : base(name, doc, Location) 
		{
			Type = new AstIdExpr("class", Location);
			Type.OutType = new ClassType(this);

			Declarations = declarations;
		}

        internal ClassDeclJson GetJson()
        {
			var fields = Declarations.Where(x => x is AstVarDecl).Select(x => (x as AstVarDecl).GetJson()).ToList();
			var attributes = Attributes.Select(x => x.GetJson()).ToList();
			return new ClassDeclJson()
            {
                Fields = fields,
                Name = Name.Name,
                SpecialKeys = SpecialKeys,
				Attributes = attributes,
                DocString = Documentation
            };
        }
    }

	// TODO: add inherited types property
	internal class ClassDeclJson
	{
		public List<VarDeclJson> Fields { get; set; }
        public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
		public List<AttributeJson> Attributes { get; set; }

		public string DocString { get; set; }
    }
}
