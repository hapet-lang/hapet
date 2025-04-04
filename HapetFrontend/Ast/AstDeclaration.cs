using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using Newtonsoft.Json;

namespace HapetFrontend.Ast
{
    public abstract class AstDeclaration : AstStatement
    {
        /// <summary>
        /// Could be anything - nested, id, genId, tuple
        /// </summary>
        public AstExpression Type { get; set; }
        public AstIdExpr Name { get; set; }

        public string Documentation { get; set; }

        /// <summary>
        /// Keys like public/static/virtual and other
        /// </summary>
        public List<Token> SpecialKeys { get; private set; } = new List<Token>();
        /// <summary>
        /// Attributes that are applied to the decl
        /// </summary>
        public List<AstAttributeStmt> Attributes { get; } = new List<AstAttributeStmt>();

        /// <summary>
        /// The inner scope of the decl. Used to get access to it's content
		/// Not for every decl!!!
        /// </summary>
        public Scope SubScope { get; set; }

        /// <summary>
        /// The class/struct/interface/other_shite that contains the decl
        /// </summary>
        [JsonIgnore]
        public AstDeclaration ContainingParent { get; set; }

        /// <summary>
        /// 'true' if the declaration is a generic decl like 'List-T-'
        /// </summary>
        public bool HasGenericTypes { get; set; }

        /// <summary>
        /// 'true' if smth like List-T- or List-int-, 'false' on real pure generic type
        /// </summary>
        public bool IsImplOfGeneric { get; set; }

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
        /// Contains the original generic decl from which the current one is created 
        /// also if the current one is <see cref="IsImplOfGeneric"/>
        /// </summary>
        public AstDeclaration OriginalGenericDecl { get; set; }

        /// <summary>
        /// 'true' if the decl is nested decl
        /// </summary>
        public bool IsNestedDecl { get; set; }
        /// <summary>
        /// Contains parent decl if the <see cref="IsNestedDecl"/> is 'true'
        /// </summary>
        public AstDeclaration ParentDecl { get; set; }

        /// <summary>
        /// Getting symbol of itself
        /// </summary>
        [JsonIgnore]
        public virtual ISymbol GetSymbol
        {
            get
            {
                return Scope.GetSymbol(Name.Name);
            }
        }

        /// <summary>
        /// Is the declaration imported from another assembly
        /// </summary>
        public bool IsImported { get; set; }

        public override string AAAName => nameof(AstDeclaration);

        public AstDeclaration(AstIdExpr name, string doc, ILocation location = null) : base(location)
        {
            this.Name = name;
            this.Documentation = doc;
        }

        /// <summary>
        /// Returns the real struct size for allocation
        /// Could be used only after <see cref="GenerateTypeInfoConst"/> from backend
        /// </summary>
        /// <returns>The size</returns>
        public static int GetSizeForAlloc(List<AstDeclaration> fields, bool withTypeInfo = true)
        {
            if (withTypeInfo)
            {
                var tp = new AstIdExpr("uintptr") { OutType = PointerType.VoidLiteralType };
                fields.Insert(0, new AstVarDecl(new AstNestedExpr(tp, null) { OutType = tp.OutType }, new AstIdExpr("typeinfo")));
            }
            int totalSize = 0;
            // go all over the fields and calc the size
            for (int i = 0; i < fields.Count; ++i)
            {
                var field = fields[i];
                var fieldType = field.Type.OutType;

                int fieldAlignment = fieldType.GetAlignment() == 0 ? fieldType.GetSize() : fieldType.GetAlignment();
                int padding = (fieldAlignment - (totalSize % fieldAlignment)) % fieldAlignment;  // Alignment
                totalSize += padding;  // Add padding for the alignment
                totalSize += fieldType.GetSize();  // Add field size
            }
            return totalSize;
        }

        /// <summary>
        /// Helper function that returns inherited shite for class and struct decls
        /// </summary>
        /// <returns>Inhertied types</returns>
        public List<AstNestedExpr> GetInheritedTypes()
        {
            if (this is AstClassDecl clsDecl)
                return clsDecl.InheritedFrom;
            else if (this is AstStructDecl strDecl)
                return strDecl.InheritedFrom;
            return new List<AstNestedExpr>();
        }
    }
}
