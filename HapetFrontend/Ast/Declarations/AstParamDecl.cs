using HapetFrontend.Ast.Expressions;
using HapetFrontend.Enums;

namespace HapetFrontend.Ast.Declarations
{
    public class AstParamDecl : AstDeclaration
    {
        /// <summary>
        /// Default value of the parameter
        /// </summary>
        public AstExpression DefaultValue { get; set; }

        /// <summary>
        /// The parameter modificator like 'ref', 'params', etc.
        /// </summary>
        public ParameterModificator ParameterModificator { get; set; } = ParameterModificator.None;

        /// <summary>
        /// Used in LSP
        /// </summary>
        public ILocation ParamModificatorLocation { get; set; }

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
                Name?.GetDeepCopy() as AstIdExpr,
                DefaultValue?.GetDeepCopy() as AstExpression,
                Documentation, Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                ParameterModificator = ParameterModificator,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
                ParamModificatorLocation = ParamModificatorLocation
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);
            return copy;
        }
    }
}
