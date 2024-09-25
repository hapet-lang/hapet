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

		private AstNestedIdExpr ParseIdentifierExpression(ErrorMessageResolver customErrorMessage = null, TokenType identType = TokenType.Identifier)
		{
			var next = PeekToken();
			if (next.Type != identType)
			{
				ReportError(next.Location, customErrorMessage?.Invoke(next) ?? "Expected identifier");
				return new AstNestedIdExpr("anon", null, new Location(next.Location));
			}
			NextToken();

			StringBuilder sb = new StringBuilder();
			sb.Append((string)next.Data);
			// while there are more idents or periods
			while (CheckToken(TokenType.Period))
			{
				NextToken();
				sb.Append('.');
				if (CheckToken(identType))
				{
					next = NextToken();
					sb.Append((string)next.Data);
				}
				else
				{
					ReportError(PeekToken().Location, "Expected identifier after '.'");
				}
			}

			return (new AstIdExpr(sb.ToString(), new Location(next.Location))).ToNested();
		}
	}
}
