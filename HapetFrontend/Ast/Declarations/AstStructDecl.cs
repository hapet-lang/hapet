using HapetFrontend.Ast.Expressions;
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
		/// The inner scope of the struct. Used to get access to it's content
		/// </summary>
		public Scope SubScope { get; set; }

		public AstStructDecl(AstIdExpr name, string doc = "", ILocation Location = null) : base(name, doc, Location)
		{
			Type = new AstIdExpr("struct", Location);
			Type.OutType = new StructType(this);
		}

        internal StructDeclJson GetJson()
        {
            var fields = Declarations.Where(x => x is AstVarDecl).Select(x => (x as AstVarDecl).GetJson()).ToList();
            return new StructDeclJson()
            {
                Fields = fields,
                Name = Name.Name,
                SpecialKeys = SpecialKeys,
                DocString = Documentation
            };
        }
    }

    internal class StructDeclJson
    {
        public List<VarDeclJson> Fields { get; set; }
        public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }

        public string DocString { get; set; }
    }
}
