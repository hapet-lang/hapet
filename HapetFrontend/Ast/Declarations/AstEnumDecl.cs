using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using System.Numerics;

namespace HapetFrontend.Ast.Declarations
{
    public class AstEnumDecl : AstDeclaration
    {
        /// <summary>
        /// Declarations that are in the enum (their type should be inherited from the enum type)
        /// </summary>
        public List<AstVarDecl> Declarations { get; } = new List<AstVarDecl>();

        /// <summary>
        /// The numeric type from which the enum inherits. Default is 'int'
        /// </summary>
        public AstNestedExpr InheritedType { get; set; }

        public override string AAAName => nameof(AstEnumDecl);

        public AstEnumDecl(AstIdExpr name, List<AstVarDecl> declarations, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = new AstIdExpr("enum", location);
            Type.OutType = new EnumType(this);

            Declarations = declarations;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstEnumDecl(
                Name.GetDeepCopy() as AstIdExpr,
                Declarations.Select(x => x.GetDeepCopy() as AstVarDecl).ToList(),
                Documentation, Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                InheritedType = InheritedType.GetDeepCopy() as AstNestedExpr,
                IsNestedDecl = IsNestedDecl,
                ParentDecl = ParentDecl,
                IsImported = IsImported,
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
