using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
    public class AstStructDecl : AstDeclaration
    {
        /// <summary>
        /// Declarations that are in the struct
        /// </summary>
        public List<AstDeclaration> Declarations { get; } = new List<AstDeclaration>();

        /// <summary>
        /// The list of types from which the current struct is inherited
        /// </summary>
        public List<AstNestedExpr> InheritedFrom { get; set; } = new List<AstNestedExpr>();

        /// <summary>
        /// All original raw fields (including inherited) (for easier interface offset generation)
        /// </summary>
        public List<AstVarDecl> AllRawFields { get; set; }
        /// <summary>
        /// All original raw props (including inherited) (for easier inference)
        /// </summary>
        public List<AstPropertyDecl> AllRawProps { get; set; }
        /// <summary>
        /// All original virtual methods (including inherited) 
        /// </summary>
        public List<AstFuncDecl> AllVirtualMethods { get; set; }

        public override string AAAName => nameof(AstStructDecl);

        public AstStructDecl(AstIdExpr name, List<AstDeclaration> declarations, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = new AstIdExpr("struct", Location);
            Type.OutType = new StructType(this);

            Declarations = declarations;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstStructDecl(
                Name.GetDeepCopy() as AstIdExpr,
                Declarations.Select(x => x.GetDeepCopy() as AstDeclaration).ToList(),
                Documentation, Location)
            {
                AllRawFields = AllRawFields.Select(x => x.GetDeepCopy() as AstVarDecl).ToList(),
                AllRawProps = AllRawProps.Select(x => x.GetDeepCopy() as AstPropertyDecl).ToList(),
                AllVirtualMethods = AllVirtualMethods.Select(x => x.GetDeepCopy() as AstFuncDecl).ToList(),
                InheritedFrom = InheritedFrom.Select(x => x.GetDeepCopy() as AstNestedExpr).ToList(),
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
    }
}
