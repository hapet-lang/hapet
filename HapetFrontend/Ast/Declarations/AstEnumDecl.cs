using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using System.Numerics;

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

		internal EnumDeclJson GetJson()
		{
			var fields = Declarations.Select(x => x.Name.Name).ToList();
			var values = Declarations.Select(x => ((NumberData)(x.Initializer.OutValue)).IntValue).ToList();
			var attributes = Attributes.Select(x => x.GetJson()).ToList();
			return new EnumDeclJson()
			{
				Fields = fields,
				Values = values,
				Name = Name.Name,
				SpecialKeys = SpecialKeys,
				Attributes = attributes,
				DocString = Documentation
			};
		}
	}

	internal class EnumDeclJson
	{
		public List<string> Fields { get; set; }
		public List<BigInteger> Values { get; set; }
		public string Name { get; set; }

		public List<TokenType> SpecialKeys { get; set; }
		public List<AttributeJson> Attributes { get; set; }

		public string DocString { get; set; }
	}
}
