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

			var expr = ParseExpression(true, false, ErrMsg("expression", "after keyword 'return'"));

			if (expr is not AstExpression)
			{
				ReportError(expr.Location, "Expression expected after 'return' keyword");
				return ParseEmptyExpression();
			}

			return new AstReturnStmt(expr as AstExpression, Location: new Location(beg));
		}
	}
}
