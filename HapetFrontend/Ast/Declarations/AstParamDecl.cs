using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using Newtonsoft.Json;

namespace HapetFrontend.Ast.Declarations
{
    public class AstParamDecl : AstDeclaration
    {
        /// <summary>
        /// Default value of the parameter
        /// </summary>
        public AstExpression DefaultValue { get; set; }

        public override string AAAName => nameof(AstParamDecl);

        public AstParamDecl(AstExpression type, AstIdExpr name, AstExpression defaultValue = null, string doc = "", ILocation Location = null) : base(name, doc, Location)
        {
            Type = type;
            DefaultValue = defaultValue;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstParamDecl(
                Type.GetDeepCopy() as AstExpression, 
                Name.GetDeepCopy() as AstIdExpr,
                DefaultValue?.GetDeepCopy() as AstExpression,
                Documentation, Location)
            {
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);
            return copy;
        }

        public AstVarDecl ToVarDecl()
        {
            var varDecl = new AstVarDecl(Type, Name, DefaultValue, Documentation, Location);
            return varDecl;
        }

        public AstParamDecl GetCopy(string name = "")
        {
            AstIdExpr nm = Name;
            if (!string.IsNullOrWhiteSpace(name))
                nm = Name.GetCopy(name);

            var np = new AstParamDecl(Type, nm, DefaultValue, Documentation, Location);
            np.SpecialKeys.AddRange(SpecialKeys);
            np.Attributes.AddRange(Attributes);
            return np;
        }

        internal ParamDeclJson GetJson()
        {
            var attributes = Attributes.Select(x => x.GetJson()).ToList();
            return new ParamDeclJson()
            {
                Type = HapetType.AsString(Type.OutType),
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

        public AstParamDecl GetAst(Compiler compiler)
        {
            var pr = new AstParamDecl(Parser.ParseType(Type, compiler), new AstIdExpr(Name), null, DocString);
            pr.SpecialKeys.AddRange(SpecialKeys);
            pr.Attributes.AddRange(Attributes.Select(x => x.GetAst(compiler)));
            return pr;
        }
    }
}
