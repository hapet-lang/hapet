using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using Newtonsoft.Json;
using System.Text;

namespace HapetFrontend.Ast.Declarations
{
    public class AstFuncDecl : AstDeclaration
    {
        public CallingConvention CallingConvention { get; set; } = CallingConvention.Default;
        public ClassFunctionType ClassFunctionType { get; set; } = ClassFunctionType.Default;

        public List<AstParamDecl> Parameters { get; set; }
        public AstExpression Returns { get; set; }

        [JsonIgnore]
        public AstBlockExpr Body { get; set; }

        /// <summary>
        /// The class that contains the function
        /// </summary>
        [JsonIgnore]
        public AstDeclaration ContainingParent { get; set; }

        /// <summary>
        /// Statement of calling base ctor. Used only for ctors!!!!
        /// </summary>
        public AstBaseCtorStmt BaseCtorCall { get; set; }

        /// <summary>
        /// Used for easier infferencing. Mean that the func is a get/set func
        /// </summary>
        public bool IsPropertyFunction { get; set; }

        public AstFuncDecl(List<AstParamDecl> parameters, AstExpression returns, AstBlockExpr body, AstIdExpr name, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = new AstIdExpr("func", location);
            Type.OutType = new FunctionType(this);

            Body = body;
            Parameters = parameters;
            Returns = returns;
        }

        public FuncDeclJson GetJson()
        {
            var parameters = Parameters.Select(x => x.GetJson()).ToList();
            var attributes = Attributes.Select(x => x.GetJson()).ToList();
            return new FuncDeclJson()
            {
                Parameters = parameters,
                ReturnType = HapetType.AsString(Returns.OutType),
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

        public AstFuncDecl GetAst(Compiler compiler)
        {
            var decl = new AstFuncDecl(Parameters.Select(x => x.GetAst(compiler)).ToList(), Parser.ParseType(ReturnType, compiler), null, new AstIdExpr(Name), DocString);
            decl.SpecialKeys.AddRange(SpecialKeys);
            decl.Attributes.AddRange(Attributes.Select(x => x.GetAst(compiler)));
            decl.CallingConvention = CallingConvention;
            return decl;
        }
    }
}
