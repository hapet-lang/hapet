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

        /// <summary>
        /// 'true' if it has 'params' word
        /// </summary>
        public bool IsParams { get; set; }

        public override string AAAName => nameof(AstParamDecl);

        public AstParamDecl(AstExpression type, AstIdExpr name, AstExpression defaultValue = null, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = type;
            DefaultValue = defaultValue;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstParamDecl(
                Type.GetDeepCopy() as AstNestedExpr, 
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
    }
}
