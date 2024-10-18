using HapetFrontend.Ast;
using HapetFrontend.Ast.Statements;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		public AstStatement ParseReturnStatement()
		{
			TokenLocation beg = null;

			beg = Consume(TokenType.KwReturn, ErrMsg("keyword 'return'", "at beginning of 'return' statement")).Location;
			SkipNewlines();

			// if it is a simple return without params
			if (CheckToken(TokenType.Semicolon))
			{
				return new AstReturnStmt(null, Location: new Location(beg));
			}

			var expr = ParseExpression(true, false, ErrMsg("expression", "after keyword 'return'"));
			// here is the check for AstEmptyStmt because ParseExpression
			// will already generate an exception for this and return AstEmptyStmt
			// so there is no need to generate exception twice :)
			if (expr is not AstExpression && expr is not AstEmptyStmt)
			{
				ReportError(expr.Location, "Code ");
				return ParseEmptyExpression();
			}

			return new AstReturnStmt(expr as AstExpression, Location: new Location(beg));
		}
	}
}
