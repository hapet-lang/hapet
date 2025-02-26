using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
    /// <summary>
    /// Used when it is not really a decl. Should not go further than Parser
    /// For example used when attributes are parsed in ClassPart
    /// </summary>
    public class AstEmptyDecl : AstDeclaration
    {
        public override string AAAName => nameof(AstEmptyDecl);

        public AstEmptyDecl(AstIdExpr name, string doc = "", ILocation Location = null) : base(name, doc, Location)
        {
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstEmptyDecl(
                Name.GetDeepCopy() as AstIdExpr,
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
    }
}
