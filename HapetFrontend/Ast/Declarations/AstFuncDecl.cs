using HapetFrontend.Ast.Expressions;
using HapetFrontend.Enums;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using System.Text;

namespace HapetFrontend.Ast.Declarations
{
    public class AstFuncDecl : AstDeclaration
	{
		public CallingConvention CallingConvention { get; } = CallingConvention.Default;

		public List<ClassFunctionType> ClassFunctionTypes { get; } = new List<ClassFunctionType>();

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
            return new FuncDeclJson()
            {
                Parameters = parameters,
                ReturnType = Returns.OutType.ToString(),
                Name = Name.Name,
                SpecialKeys = SpecialKeys,
                CallingConvention = CallingConvention,
                DocString = Documentation
            };
        }
    }

    internal class FuncDeclJson
    {
        public List<ParamDeclJson> Parameters { get; set; }
        public string ReturnType { get; set; }
        public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }

		public CallingConvention CallingConvention { get; set; }

        public string DocString { get; set; }
    }
}
