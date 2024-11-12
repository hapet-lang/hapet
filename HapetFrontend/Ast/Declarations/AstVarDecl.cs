using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
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

		internal VarDeclJson GetJson()
		{
			var attributes = Attributes.Select(x => x.GetJson()).ToList();
			return new VarDeclJson()
			{
				Type = Type.OutType.ToString(),
				Name = Name.Name,
				SpecialKeys = SpecialKeys,
				Attributes = attributes,
				DocString = Documentation
			};
		}
    }

    public class VarDeclJson
    {
		public string Type { get; set; }
		public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
		public List<AttributeJson> Attributes { get; set; }

		public string DocString { get; set; }
    }
}
