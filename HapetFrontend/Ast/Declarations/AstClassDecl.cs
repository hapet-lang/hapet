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
        /// Generic name aliases like T in:
        /// public class TestCls-T- { ... }
        /// </summary>
        public List<AstIdExpr> GenericNames { get; set; } = new List<AstIdExpr>();

        /// <summary>
        /// Generic parameter constrains like:
        /// ...-T- where T: struct, enum, class { ... }
        /// </summary>
        public Dictionary<AstIdExpr, List<AstNestedExpr>> GenericConstrains { get; set; } = new Dictionary<AstIdExpr, List<AstNestedExpr>>();

        /// <summary>
        /// 'true' if the declaration is an interface
        /// </summary>
        public bool IsInterface { get; set; }

        /// <summary>
        /// 'true' if the declaration is a declaration of a generic type like 'T'
        /// </summary>
        public bool IsGenericType { get; set; }

        /// <summary>
        /// 'true' if the declaration is a generic decl like 'List-T-'
        /// </summary>
        public bool HasGenericTypes { get; set; }

        /// <summary>
        /// 'true' if smth like List-T- or List-int-, 'false' on real pure generic type
        /// </summary>
        public bool IsImplOfGeneric { get; set; }

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

        public AstClassDecl(AstIdExpr name, List<AstDeclaration> declarations, string doc = "", ILocation Location = null) : base(name, doc, Location)
        {
            Type = new AstNestedExpr(new AstIdExpr("class", Location), null, Location);
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

        public ClassDeclJson GetJson()
        {
            var fields = Declarations.Where(x => x is AstVarDecl && x is not AstPropertyDecl).Select(x => (x as AstVarDecl).GetJson()).ToList();
            var inhs = InheritedFrom.Select(x => HapetType.AsString(x.OutType)).ToList();
            var props = Declarations.Where(x => x is AstPropertyDecl).Select(x => (x as AstPropertyDecl).GetJsonPropa()).ToList();
            var attributes = Attributes.Select(x => x.GetJson()).ToList();
            return new ClassDeclJson()
            {
                Fields = fields,
                Properties = props,
                Name = Name.Name,
                InheritedTypes = inhs,
                SpecialKeys = SpecialKeys,
                Attributes = attributes,
                DocString = Documentation
            };
        }
    }

    public class ClassDeclJson
    {
        public List<VarDeclJson> Fields { get; set; }
        public List<PropertyDeclJson> Properties { get; set; }
        public string Name { get; set; }

        public List<string> InheritedTypes { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
        public List<AttributeJson> Attributes { get; set; }

        public string DocString { get; set; }

        public AstClassDecl GetAst(Compiler compiler)
        {
            var allClassDecls = new List<AstDeclaration>();
            allClassDecls.AddRange(Fields.Select(x => x.GetAst(compiler)));
            allClassDecls.AddRange(Properties.Select(x => x.GetAst(compiler)));
            var decl = new AstClassDecl(new AstIdExpr(Name), allClassDecls, DocString);
            decl.SpecialKeys.AddRange(SpecialKeys);
            decl.Attributes.AddRange(Attributes.Select(x => x.GetAst(compiler)));
            decl.InheritedFrom.AddRange(InheritedTypes.Select(x => new AstNestedExpr(new AstIdExpr(x), null)));
            return decl;
        }
    }
}
