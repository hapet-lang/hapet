using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
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
        /// 'true' if the declaration is a declaration of a generic type like 'T'
        /// </summary>
        public bool IsGenericType { get; set; }

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

        public override string AAAName => nameof(AstClassDecl);

        public AstClassDecl(AstIdExpr name, List<AstDeclaration> declarations, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = new AstIdExpr("class", location);
            Type.OutType = new ClassType(this);

            Declarations = declarations;
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
                AllRawProps = AllRawProps?.Select(x => x.GetDeepCopy() as AstPropertyDecl).ToList(),
                AllVirtualMethods = AllVirtualMethods?.Select(x => x.GetDeepCopy() as AstFuncDecl).ToList(),
                GenericNames = GenericNames?.Select(x => x.GetDeepCopy() as AstIdExpr).ToList(),
                GenericConstrains = copiedConstrains,
                HasGenericTypes = HasGenericTypes,
                InheritedFrom = InheritedFrom?.Select(x => x.GetDeepCopy() as AstNestedExpr).ToList(),
                IsGenericType = IsGenericType,
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
            return $"{Name}";
        }
    }
}
