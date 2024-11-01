using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;

namespace HapetFrontend.Ast.Declarations
{
	/// <summary>
	/// Ast for properties:
	/// Prop { get; set; }				=> (field_Prop, get_Prop(), set_Prop(...))
	/// Prop { get; }					=> (field_Prop, get_Prop())
	/// Prop { set; }					=> could not be, error
	/// Prop { get {...} set {...} }	=> (get_Prop(), set_Prop(...))
	/// Prop { get {...} }				=> (get_Prop())
	/// Prop { set {...} }				=> (set_Prop(...))
	/// Prop { get {...} set; }			=> could not be, error
	/// Prop { get; set {...} }			=> could not be, error
	/// </summary>
	public class AstPropertyDecl : AstVarDecl
	{
		/// <summary>
		/// True if 'get' is declared
		/// </summary>
		public bool HasGet { get; set; }
		/// <summary>
		/// True if 'set' is declared
		/// </summary>
		public bool HasSet { get; set; }

		/// <summary>
		/// Block for 'get'. Could be null
		/// </summary>
		public AstBlockExpr GetBlock { get; set; }
		/// <summary>
		/// Block for 'set'. Could be null
		/// </summary>
		public AstBlockExpr SetBlock { get; set; }

		public AstPropertyDecl(AstExpression type, AstIdExpr name, AstExpression ini = null, string doc = "", ILocation Location = null) : base(type, name, ini, doc, Location)
		{
		}

		internal PropertyDeclJson GetJsonPropa()
		{
			var attributes = Attributes.Select(x => x.GetJson()).ToList();
			return new PropertyDeclJson()
			{
				Type = Type.OutType.ToString(),
				Name = Name.Name,
				HasGet = HasGet,
				HasSet = HasSet,
				SpecialKeys = SpecialKeys,
				Attributes = attributes,
				DocString = Documentation
			};
		}
	}

	internal class PropertyDeclJson
	{
		public string Type { get; set; }
		public string Name { get; set; }

		public bool HasGet { get; set; }
		public bool HasSet { get; set; }

		public List<TokenType> SpecialKeys { get; set; }
		public List<AttributeJson> Attributes { get; set; }

		public string DocString { get; set; }
	}
}
