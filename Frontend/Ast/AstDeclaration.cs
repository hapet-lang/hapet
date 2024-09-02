using Frontend.Ast.Expressions;
using Frontend.Parsing.Entities;
using Frontend.Scoping.Entities;
using Frontend.Types;

namespace Frontend.Ast
{
	public abstract class AstDeclaration : AstStatement, ITypedSymbol
	{
		public AstIdExpr Name { get; set; }
		public HapetType Type { get; set; }

		public HashSet<AstDeclaration> Dependencies { get; set; } = new HashSet<AstDeclaration>();

		string ISymbol.Name => Name.Name;

		public bool IsUsed { get; set; }

		public string Documentation { get; set; }

		public AstDeclaration(AstIdExpr name, string doc, List<AstDirective> Directives = null, ILocation Location = null) : base(Directives, Location)
		{
			this.Name = name;
			this.Documentation = doc;
		}
	}
}
