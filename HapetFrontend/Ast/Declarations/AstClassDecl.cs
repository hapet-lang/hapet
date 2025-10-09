using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using System.Collections.Generic;

namespace HapetFrontend.Ast.Declarations
{
    public class AstClassDecl : AstDeclaration
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
        /// 'true' if the declaration is an interface
        /// </summary>
        public bool IsInterface { get; set; }

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

        public override string AAAName => nameof(AstClassDecl);

        public AstClassDecl(AstIdExpr name, List<AstDeclaration> declarations, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = new AstIdExpr("class", location);
            Type.OutType = new ClassType(this);

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

        private AstClassDecl CreateCopyInternal(bool doDeepCopy)
        {
            var copy = new AstClassDecl(
               Name.GetDeepCopy() as AstIdExpr,
               new List<AstDeclaration>(),
               Documentation, Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                HasGenericTypes = HasGenericTypes,
                IsInterface = IsInterface,
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
            return GenericsHelper.GetNameFromAst(Name.GetCopy(NameWithNs), null);
        }
    }
}
