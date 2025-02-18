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

        public AstStructDecl(AstIdExpr name, List<AstDeclaration> declarations, string doc = "", ILocation Location = null) : base(name, doc, Location)
        {
            Type = new AstIdExpr("struct", Location);
            Type.OutType = new StructType(this);

            Declarations = declarations;
        }

        public StructDeclJson GetJson()
        {
            var fields = Declarations.Where(x => x is AstVarDecl && x is not AstPropertyDecl).Select(x => (x as AstVarDecl).GetJson()).ToList();
            var inhs = InheritedFrom.Select(x => HapetType.AsString(x.OutType)).ToList();
            var props = Declarations.Where(x => x is AstPropertyDecl).Select(x => (x as AstPropertyDecl).GetJsonPropa()).ToList();
            var attributes = Attributes.Select(x => x.GetJson()).ToList();
            return new StructDeclJson()
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

    public class StructDeclJson
    {
        public List<VarDeclJson> Fields { get; set; }
        public List<PropertyDeclJson> Properties { get; set; }
        public string Name { get; set; }

        public List<string> InheritedTypes { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
        public List<AttributeJson> Attributes { get; set; }

        public string DocString { get; set; }

        public AstStructDecl GetAst(Compiler compiler)
        {
            var allDecls = new List<AstDeclaration>();
            allDecls.AddRange(Fields.Select(x => x.GetAst(compiler)));
            allDecls.AddRange(Properties.Select(x => x.GetAst(compiler)));
            var decl = new AstStructDecl(new AstIdExpr(Name), allDecls, DocString);
            decl.SpecialKeys.AddRange(SpecialKeys);
            decl.Attributes.AddRange(Attributes.Select(x => x.GetAst(compiler)));
            decl.InheritedFrom.AddRange(InheritedTypes.Select(x => new AstNestedExpr(new AstIdExpr(x), null)));
            return decl;
        }
    }
}
