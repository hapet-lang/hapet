using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
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
        /// All original virtual methods (including inherited) 
        /// </summary>
        public List<AstFuncDecl> AllVirtualMethods { get; set; }
        /// <summary>
        /// Used by get/set generator to be able to put these get/set's into <see cref="AllVirtualMethods"/>
        /// </summary>
        public List<AstPropertyDecl> AllVirtualProps { get; set; }

        public override string AAAName => nameof(AstStructDecl);

        public AstStructDecl(AstIdExpr name, List<AstDeclaration> declarations, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = new AstIdExpr("struct", Location);
            Type.OutType = new StructType(this);

            Declarations = declarations;
        }

        public override AstDeclaration GetOnlyDeclareCopy()
        {
            var copy = CreateCopyInternal(false);
            return copy;
        }

        public override AstStatement GetDeepCopy()
        {
            Dictionary<AstIdExpr, List<AstConstrainStmt>> copiedConstrains = new Dictionary<AstIdExpr, List<AstConstrainStmt>>();
            foreach (var cc in GenericConstrains)
            {
                copiedConstrains.Add(cc.Key.GetDeepCopy() as AstIdExpr, cc.Value.Select(x => x.GetDeepCopy() as AstConstrainStmt).ToList());
            }

            var copy = CreateCopyInternal(true);
            copy.GenericConstrains = copiedConstrains;
            return copy;
        }

        private AstStructDecl CreateCopyInternal(bool doDeepCopy)
        {
            var copy = new AstStructDecl(
                Name.GetDeepCopy() as AstIdExpr,
                new List<AstDeclaration>(),
                Documentation, Location)
            {
                HasGenericTypes = HasGenericTypes,
                IsImported = IsImported,
                IsNestedDecl = IsNestedDecl,
                ParentDecl = ParentDecl,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);

            copy.InheritedFrom = InheritedFrom?.Select(x => x.GetDeepCopy() as AstNestedExpr).ToList();

            // do deep copy of containings if deepCopy selected
            if (doDeepCopy)
            {
                copy.AllRawFields = AllRawFields?.Select(x => x.GetDeepCopy() as AstVarDecl).ToList();
                copy.AllVirtualMethods = AllVirtualMethods?.Select(x => x.GetDeepCopy() as AstFuncDecl).ToList();
                copy.Declarations.AddRange(Declarations.Select(x => x.GetDeepCopy() as AstDeclaration));
            }
            else
            {
                copy.Declarations.AddRange(Declarations.Select(x => x.GetOnlyDeclareCopy()));
            }

            // handle containing parent shite
            foreach (var decl in copy.Declarations)
            {
                if (decl is AstVarDecl vD)
                    vD.ContainingParent = copy;
                else if (decl is AstFuncDecl fD)
                    fD.ContainingParent = copy;

                if (decl.IsNestedDecl)
                    decl.ParentDecl = copy;
            }

            return copy;
        }

        public override string ToString()
        {
            return GenericsHelper.GetNameFromAst(Name, null);
        }
    }
}
