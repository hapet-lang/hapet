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

        public AstStructDecl(AstIdExpr name, List<AstDeclaration> declarations, string doc = "", ILocation Location = null) : base(name, doc, Location)
        {
            Type = new AstIdExpr("struct", Location);
            Type.OutType = new StructType(this);

            Declarations = declarations;
        }

        internal StructDeclJson GetJson()
        {
            var fields = Declarations.Where(x => x is AstVarDecl).Select(x => (x as AstVarDecl).GetJson()).ToList();
            var attributes = Attributes.Select(x => x.GetJson()).ToList();
            return new StructDeclJson()
            {
                Fields = fields,
                Name = Name.Name,
                SpecialKeys = SpecialKeys,
                Attributes = attributes,
                DocString = Documentation
            };
        }
    }

    public class StructDeclJson
    {
        public List<VarDeclJson> Fields { get; set; }
        public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
        public List<AttributeJson> Attributes { get; set; }

        public string DocString { get; set; }

        public AstStructDecl GetAst()
        {
            var allDecls = new List<AstDeclaration>();
            allDecls.AddRange(Fields.Select(x => x.GetAst()));
            var decl = new AstStructDecl(new AstIdExpr(Name), allDecls, DocString);
            decl.SpecialKeys.AddRange(SpecialKeys);
            decl.Attributes.AddRange(Attributes.Select(x => x.GetAst()));
            return decl;
        }
    }
}
