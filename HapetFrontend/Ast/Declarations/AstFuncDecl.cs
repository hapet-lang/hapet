using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using System.Text;

namespace HapetFrontend.Ast.Declarations
{
    public class AstFuncDecl : AstDeclaration
	{
		public CallingConvention CallingConvention { get; set; } = CallingConvention.Default;
        public ClassFunctionType ClassFunctionType { get; set; } = ClassFunctionType.Default;

		public List<AstParamDecl> Parameters { get; set; }
		public AstExpression Returns { get; set; }

		public AstBlockExpr Body { get; set; }

		/// <summary>
		/// The class that contains the function
		/// </summary>
		public AstClassDecl ContainingClass { get; set; }

		public AstFuncDecl(List<AstParamDecl> parameters, AstExpression returns, AstBlockExpr body, AstIdExpr name, string doc = "", ILocation Location = null) : base(name, doc, Location)
		{
			Type = new AstIdExpr("func", Location);
			Type.OutType = new FunctionType(this);

			Body = body;
			Parameters = parameters;
			Returns = returns;
		}

        internal FuncDeclJson GetJson()
        {
            var parameters = Parameters.Select(x => x.GetJson()).ToList();
            var attributes = Attributes.Select(x => x.GetJson()).ToList();
            return new FuncDeclJson()
            {
                Parameters = parameters,
                ReturnType = Returns.OutType.ToString(),
                Name = Name.Name,
                SpecialKeys = SpecialKeys,
                Attributes = attributes,
				CallingConvention = CallingConvention,
                DocString = Documentation
            };
        }
    }

    public class FuncDeclJson
    {
        public List<ParamDeclJson> Parameters { get; set; }
        public string ReturnType { get; set; }
        public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
        public List<AttributeJson> Attributes { get; set; }

		public CallingConvention CallingConvention { get; set; }

        public string DocString { get; set; }

        public AstFuncDecl GetAst()
        {
            var decl = new AstFuncDecl(Parameters.Select(x => x.GetAst()).ToList(), new AstIdExpr(ReturnType), null, new AstIdExpr(Name), DocString);
            decl.SpecialKeys.AddRange(SpecialKeys);
            decl.Attributes.AddRange(Attributes.Select(x => x.GetAst()));
            decl.CallingConvention = CallingConvention;
			return decl;
        }
	}
}
