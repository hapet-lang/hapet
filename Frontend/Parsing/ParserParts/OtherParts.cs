using Frontend.Ast.Expressions;
using Frontend.Ast;
using Frontend.Parsing.Entities;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		public AstExpression ParseEmptyExpression()
		{
			var loc = GetWhitespaceLocation();
			return new AstEmptyExpr(new Location(loc.beg, loc.end));
		}
	}
}
