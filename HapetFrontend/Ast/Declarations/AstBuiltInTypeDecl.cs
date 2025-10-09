using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
    /// <summary>
    /// Ast class for built in types (use it only in <see cref="HapetFrontend.Scoping.Scope"/>)
    /// </summary>
    public class AstBuiltInTypeDecl : AstDeclaration
    {
        public override string AAAName => nameof(AstBuiltInTypeDecl);

        public AstBuiltInTypeDecl(HapetType tp, string doc = "", ILocation location = null) : base(null, doc, location)
        {
            Type = new AstIdExpr(tp.TypeName, location);
            Type.OutType = tp;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstBuiltInTypeDecl(Type.OutType, Documentation, Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
                GenericConstrainLocations = GenericConstrainLocations,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);
            return copy;
        }
    }
}
