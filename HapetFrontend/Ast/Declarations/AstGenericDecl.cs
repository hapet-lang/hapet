using HapetFrontend.Ast.Expressions;
using HapetFrontend.Helpers;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
    public class AstGenericDecl : AstDeclaration
    {
        /// <summary>
        /// Declarations that are in the class
        /// </summary>
        public List<AstDeclaration> Declarations { get; } = new List<AstDeclaration>();

        /// <summary>
        /// The list of types from which the current class is inherited
        /// </summary>
        public List<AstNestedExpr> InheritedFrom { get; set; } = new List<AstNestedExpr>();

        /// <summary>
        /// The list of generic constrains
        /// </summary>
        public List<AstNestedExpr> Constrains { get; set; } = new List<AstNestedExpr>();

        public override string AAAName => nameof(AstGenericDecl);

        public AstGenericDecl(AstIdExpr name, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = new AstIdExpr("generic", location);
            Type.OutType = new GenericType(this);
        }

        public override AstStatement GetDeepCopy()
        {
            Dictionary<AstIdExpr, List<AstNestedExpr>> copiedConstrains = new Dictionary<AstIdExpr, List<AstNestedExpr>>();
            foreach (var cc in GenericConstrains)
            {
                copiedConstrains.Add(cc.Key.GetDeepCopy() as AstIdExpr, cc.Value.Select(x => x.GetDeepCopy() as AstNestedExpr).ToList());
            }

            var copy = new AstClassDecl(
                Name.GetDeepCopy() as AstIdExpr,
                Declarations.Select(x => x.GetDeepCopy() as AstDeclaration).ToList(),
                Documentation, Location)
            {
                AllRawFields = AllRawFields?.Select(x => x.GetDeepCopy() as AstVarDecl).ToList(),
                AllVirtualMethods = AllVirtualMethods?.Select(x => x.GetDeepCopy() as AstFuncDecl).ToList(),
                GenericConstrains = copiedConstrains,
                HasGenericTypes = HasGenericTypes,
                InheritedFrom = InheritedFrom?.Select(x => x.GetDeepCopy() as AstNestedExpr).ToList(),
                IsInterface = IsInterface,
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
