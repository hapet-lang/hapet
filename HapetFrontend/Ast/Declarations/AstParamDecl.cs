using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using Newtonsoft.Json;

namespace HapetFrontend.Ast.Declarations
{
    public class AstParamDecl : AstDeclaration
    {
        /// <summary>
        /// Default value of the parameter
        /// </summary>
        public AstExpression DefaultValue { get; set; }

        /// <summary>
        /// The function in which the parameter presented
        /// </summary>
        [JsonIgnore]
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
            var attributes = Attributes.Select(x => x.GetJson()).ToList();
            return new ParamDeclJson()
            {
                Type = Type.OutType.ToString(),
                Name = Name.Name,
                SpecialKeys = SpecialKeys, // need it for 'ref', 'out' and other shite
                Attributes = attributes,
                DocString = Documentation
            };
        }
    }

    public class ParamDeclJson
    {
        public string Type { get; set; }
        public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
        public List<AttributeJson> Attributes { get; set; }

        public string DocString { get; set; }

        public AstParamDecl GetAst()
        {
            // TODO: WARN! default value is not saving yet!!!
            var pr = new AstParamDecl(Parser.ParseType(Type), new AstIdExpr(Name), null, DocString);
            pr.SpecialKeys.AddRange(SpecialKeys);
            pr.Attributes.AddRange(Attributes.Select(x => x.GetAst()));
            return pr;
        }
    }
}
