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

		private AstIdExpr ParseIdentifierExpr(ErrorMessageResolver customErrorMessage = null, TokenType identType = TokenType.Identifier)
		{
			var next = PeekToken();
			if (next.Type != identType)
			{
				ReportError(next.Location, customErrorMessage?.Invoke(next) ?? "Expected identifier");
				// TODO: sure about §?
				return new AstIdExpr("§", false, new Location(next.Location));
			}
			NextToken();
			return new AstIdExpr((string)next.Data, false, new Location(next.Location));
		}

		private AstArgument ParseArgumentExpression()
		{
			TokenLocation beg;
			AstExpression expr;
			AstIdExpr name = null;

			var e = ParseExpression(false);
			beg = e.Beginning;

			// if next token is : then e is the name of the parameter
			if (CheckToken(TokenType.Colon))
			{
				if (e is AstIdExpr i)
				{
					name = i;
				}
				else
				{
					ReportError(e, $"Name of argument must be an identifier");
				}

				Consume(TokenType.Colon, ErrMsg(":", "after name in argument"));
				SkipNewlines();

				expr = ParseExpression(false);
			}
			else
			{
				expr = e;
			}

			return new AstArgument(expr, name, new Location(beg, expr.Ending));
		}
	}
}
