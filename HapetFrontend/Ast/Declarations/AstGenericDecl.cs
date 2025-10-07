using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
    public class AstGenericDecl : AstDeclaration
    {
        /// <summary>
        /// Declarations that are in the class
        /// </summary>
        public List<AstDeclaration> Declarations { get; set; } = new List<AstDeclaration>();

        /// <summary>
        /// The list of generic constrains
        /// </summary>
        public List<AstConstrainStmt> Constrains { get; set; } = new List<AstConstrainStmt>();

        /// <summary>
        /// The list of types from which the current generic is inherited
        /// </summary>
        public List<AstNestedExpr> InheritedFrom { get; set; } = new List<AstNestedExpr>();

        public override string AAAName => nameof(AstGenericDecl);

        public AstGenericDecl(AstIdExpr name, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = new AstIdExpr("generic", location);
            Type.OutType = new GenericType(this);
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstGenericDecl(
                Name.GetDeepCopy() as AstIdExpr,
                Documentation, Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                Constrains = Constrains?.Select(x => x.GetDeepCopy() as AstConstrainStmt).ToList(),
                Declarations = Declarations.Select(x => x.GetDeepCopy() as AstDeclaration).ToList(),
                HasGenericTypes = HasGenericTypes,
                IsImported = IsImported,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);

            // handle containing parent shite
            foreach (var decl in copy.Declarations)
            {
                if (decl is AstVarDecl vD)
                    vD.ContainingParent = copy;
                else if (decl is AstFuncDecl fD)
                    fD.ContainingParent = copy;
            }

            return copy;
        }

        public override string ToString()
        {
            return GenericsHelper.GetNameFromAst(Name, null);
        }
    }
}
