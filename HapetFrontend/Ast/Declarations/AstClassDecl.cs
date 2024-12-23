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

		public AstClassDecl(AstIdExpr name, List<AstDeclaration> declarations, string doc = "", ILocation Location = null) : base(name, doc, Location) 
		{
			Type = new AstIdExpr("class", Location);
			Type.OutType = new ClassType(this);

			Declarations = declarations;
		}

        internal ClassDeclJson GetJson()
        {
			var fields = Declarations.Where(x => x is AstVarDecl && x is not AstPropertyDecl).Select(x => (x as AstVarDecl).GetJson()).ToList();
			var props = Declarations.Where(x => x is AstPropertyDecl).Select(x => (x as AstPropertyDecl).GetJsonPropa()).ToList();
			var attributes = Attributes.Select(x => x.GetJson()).ToList();
			return new ClassDeclJson()
            {
                Fields = fields,
				Properties = props,
                Name = Name.Name,
                SpecialKeys = SpecialKeys,
				Attributes = attributes,
                DocString = Documentation
            };
        }
    }

	// TODO: add inherited types property
	public class ClassDeclJson
	{
		public List<VarDeclJson> Fields { get; set; }
		public List<PropertyDeclJson> Properties { get; set; }
        public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
		public List<AttributeJson> Attributes { get; set; }

		public string DocString { get; set; }

		public AstClassDecl GetAst()
		{
			var allClassDecls = new List<AstDeclaration>();
			allClassDecls.AddRange(Fields.Select(x => x.GetAst()));
			allClassDecls.AddRange(Properties.Select(x => x.GetAst()));
			var decl = new AstClassDecl(new AstIdExpr(Name), allClassDecls, DocString);
			decl.SpecialKeys.AddRange(SpecialKeys);
			decl.Attributes.AddRange(Attributes.Select(x => x.GetAst()));
			return decl;
		}
    }
}
