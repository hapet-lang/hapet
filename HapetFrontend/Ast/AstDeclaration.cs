using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;

namespace HapetFrontend.Ast
{
	public abstract class AstDeclaration : AstStatement
	{
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
		/// Getting symbol of itself
		/// </summary>
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
