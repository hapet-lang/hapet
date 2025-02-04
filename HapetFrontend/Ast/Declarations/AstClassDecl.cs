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

        public AstClassDecl(AstIdExpr name, List<AstDeclaration> declarations, string doc = "", ILocation Location = null) : base(name, doc, Location)
        {
            Type = new AstIdExpr("class", Location);
            Type.OutType = new ClassType(this);

            Declarations = declarations;
        }

        /// <summary>
        /// Returns the real struct size for allocation
        /// Could be used only after <see cref="GenerateTypeInfoConst"/> from backend
        /// </summary>
        /// <returns>The size</returns>
        public int GetSizeForAlloc()
        {
            var fields = Declarations.GetStructFields();
            fields.Insert(0, new AstVarDecl(new AstIdExpr("uintptr") { OutType = PointerType.NullLiteralType }, new AstIdExpr("typeinfo")));
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

        internal ClassDeclJson GetJson()
        {
            var fields = Declarations.Where(x => x is AstVarDecl && x is not AstPropertyDecl).Select(x => (x as AstVarDecl).GetJson()).ToList();
            var inhs = InheritedFrom.Select(x => x.OutType.ToString()).ToList();
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

        public AstClassDecl GetAst()
        {
            var allClassDecls = new List<AstDeclaration>();
            allClassDecls.AddRange(Fields.Select(x => x.GetAst()));
            allClassDecls.AddRange(Properties.Select(x => x.GetAst()));
            var decl = new AstClassDecl(new AstIdExpr(Name), allClassDecls, DocString);
            decl.SpecialKeys.AddRange(SpecialKeys);
            decl.Attributes.AddRange(Attributes.Select(x => x.GetAst()));
            decl.InheritedFrom.AddRange(InheritedTypes.Select(x => new AstNestedExpr(new AstIdExpr(x), null)));
            return decl;
        }
    }
}
