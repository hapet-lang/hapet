using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using Newtonsoft.Json;

namespace HapetFrontend.Ast
{
    public abstract class AstDeclaration : AstStatement
    {
        /// <summary>
        /// Could not be nested because of tuples!!!
        /// </summary>
        public AstExpression Type { get; set; }
        public AstIdExpr Name { get; set; }

        public string Documentation { get; set; }

        /// <summary>
        /// Keys like public/static/virtual and other
        /// </summary>
        public List<TokenType> SpecialKeys { get; private set; } = new List<TokenType>();
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

        public AstDeclaration(AstIdExpr name, string doc, ILocation Location = null) : base(Location)
        {
            this.Name = name;
            this.Documentation = doc;
        }
    }
}
