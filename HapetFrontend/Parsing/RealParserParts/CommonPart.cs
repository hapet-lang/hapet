using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
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

		private AstIdExpr ParseIdentifierExpression(ErrorMessageResolver customErrorMessage = null, TokenType identType = TokenType.Identifier)
		{
			var next = PeekToken();
			if (next.Type != identType)
			{
				ReportError(next.Location, customErrorMessage?.Invoke(next) ?? "Expected identifier");
				return new AstIdExpr("anon", new Location(next.Location));
			}
			NextToken();
			return new AstIdExpr((string)next.Data, new Location(next.Location));
		}
	}
}
