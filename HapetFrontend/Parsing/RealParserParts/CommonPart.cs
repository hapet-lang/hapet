using HapetFrontend.Ast;
using HapetFrontend.Ast.Statements;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		public AstStatement ParseEmptyExpression()
		{
			var loc = GetWhitespaceLocation();
			return new AstEmptyStmt(new Location(loc.beg, loc.end));
		}
	}
}
