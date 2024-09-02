using Frontend.Ast.Expressions;
using Frontend.Ast.Statements;
using Frontend.Parsing.Entities;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		private AstAttachStmt ParseAttachStatement()
		{
			var beg = Consume(TokenType.KwAttach, ErrMsg("keyword 'attach'", "at beginning of attach statement")).Location;
			SkipNewlines();
			var expr = ParseExpression(true, errorMessage: ErrMsg("expression", "after keyword 'attach'"));
			if (expr is AstUsingExpr usingExpr && usingExpr.AsWhat != null)
			{
				ReportError(usingExpr.AsWhat.Location, $"Keyword 'as' could not be used with keyword 'attach'");
			}
			return new AstAttachStmt(expr, Location: new Location(beg));
		}

		private AstUsingExpr ParseUsingExpr()
		{
			var beg = Consume(TokenType.KwUsing, null).Location;

			var path = new List<AstIdExpr>();
			// TODO: do i need string literal check?
			if (CheckToken(TokenType.StringLiteral))
			{
				var str = NextToken();
				var pathStr = str.Data as string;
				path.AddRange(pathStr.Split("/").Select(p => new AstIdExpr(p, false, new Location(str.Location))));
			}
			else
			{
				path.Add(ParseIdentifierExpr());

				while (CheckToken(TokenType.Period))
				{
					NextToken();
					path.Add(ParseIdentifierExpr());
				}
			}

			AstIdExpr asWhat = null;
			if (CheckToken(TokenType.KwAs))
			{
				NextToken();
				asWhat = ParseIdentifierExpr();
			}

			Consume(TokenType.Semicolon, ErrMsg("symbol ';'", "at the end of using statement"));

			return new AstUsingExpr(path.ToArray(), asWhat, new Location(beg, path.Last().Ending));
		}
	}
}
