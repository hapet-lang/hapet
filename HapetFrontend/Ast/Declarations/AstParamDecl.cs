using HapetFrontend.Ast.Expressions;
using HapetFrontend.Parsing;

namespace HapetFrontend.Ast.Declarations
{
	public class AstParamDecl : AstDeclaration
	{
		public AstExpression DefaultValue { get; set; }

		/// <summary>
		/// The function in which the parameter presented
		/// </summary>
		public AstFuncDecl ContainingFunction { get; set; }

		public AstParamDecl(AstExpression type, AstIdExpr name, AstExpression defaultValue = null, string doc = "", ILocation Location = null) : base(name, doc, Location)
		{
			Type = type;
			DefaultValue = defaultValue;
		}

		public AstVarDecl ToVarDecl()
		{
			var varDecl = new AstVarDecl(Type, Name, DefaultValue, Documentation, Location);
			return varDecl;
		}

        internal ParamDeclJson GetJson()
        {
            return new ParamDeclJson()
            {
                Type = Type.OutType.ToString(),
                Name = Name.Name,
                SpecialKeys = SpecialKeys,
                DocString = Documentation
            };
        }
    }

    internal class ParamDeclJson
    {
        public string Type { get; set; }
        public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }

        public string DocString { get; set; }
    }
}
