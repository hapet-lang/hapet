using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using System.Text;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		public AstStatement ParseEmptyExpression()
		{
			var loc = GetWhitespaceLocation();
			return new AstEmptyStmt(new Location(loc.beg, loc.end));
		}

		private AstNestedExpr ParseIdentifierExpression(ErrorMessageResolver customErrorMessage = null, TokenType identType = TokenType.Identifier, bool allowDots = true)
		{
			var next = PeekToken();
			if (next.Type != identType)
			{
				ReportError(next.Location, customErrorMessage?.Invoke(next) ?? "Expected identifier");
				return new AstNestedExpr(new AstIdExpr("anon", new Location(next.Location)), null, next.Location);
			}
			NextToken();

			var beg = next.Location.Beginning;
			var currNested = new AstNestedExpr(new AstIdExpr((string)next.Data, new Location(next.Location)), null, next.Location);

			// while there are more idents or periods
			while (CheckToken(TokenType.Period))
			{
				if (!allowDots)
				{
					ReportError(PeekToken().Location, "The '.' was not expected here");
				}

				NextToken();
				if (CheckToken(identType))
				{
					next = NextToken();
					var dt = new AstIdExpr((string)next.Data, new Location(next.Location));
					currNested = new AstNestedExpr(dt, currNested, new Location(beg, next.Location));
				}
				else
				{
					ReportError(PeekToken().Location, "Expected identifier after '.'");
				}
			}

			return currNested;
		}
	}
}
