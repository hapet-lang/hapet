using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Declarations
{
    public class AstDelegateDecl : AstDeclaration
    {
        public List<AstParamDecl> Parameters { get; set; }
        public AstExpression Returns { get; set; }

        public AstDelegateDecl(List<AstParamDecl> parameters, AstExpression returns, AstIdExpr name, string doc = "", ILocation Location = null) : base(name, doc, Location)
        {
            Type = new AstIdExpr("delegate", Location);
            Type.OutType = new DelegateType(this);

            Parameters = parameters;
            Returns = returns;
        }

        internal DelegateDeclJson GetJson()
        {
            var parameters = Parameters.Select(x => x.GetJson()).ToList();
            var attributes = Attributes.Select(x => x.GetJson()).ToList();
            return new DelegateDeclJson()
            {
                Parameters = parameters,
                ReturnType = HapetType.AsString(Returns.OutType),
                Name = Name.Name,
                SpecialKeys = SpecialKeys,
                Attributes = attributes,
                DocString = Documentation
            };
        }
    }

    public class DelegateDeclJson
    {
        public List<ParamDeclJson> Parameters { get; set; }
        public string ReturnType { get; set; }
        public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
        public List<AttributeJson> Attributes { get; set; }

        public string DocString { get; set; }

        public AstDelegateDecl GetAst()
        {
            var decl = new AstDelegateDecl(Parameters.Select(x => x.GetAst()).ToList(), Parser.ParseType(ReturnType), new AstIdExpr(Name), DocString);
            decl.SpecialKeys.AddRange(SpecialKeys);
            decl.Attributes.AddRange(Attributes.Select(x => x.GetAst()));
            return decl;
        }
    }
}
