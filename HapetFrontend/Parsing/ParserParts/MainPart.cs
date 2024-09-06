using HapetFrontend.Ast;
using HapetFrontend.Ast.Statements;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		public AstStatement ParseStatement(bool expectNewline = true)
		{
			var stmt = ParseStatementHelper();

			if (stmt == null)
				return null;

			if (CheckToken(TokenType.Semicolon))
			{
				// TODO: haha is this cringe?
				CheckToken(TokenType.Semicolon);
			}

			var next = PeekToken();
			if (expectNewline && next.Type != TokenType.NewLine && next.Type != TokenType.EOF)
			{
				ReportError(next.Location, $"Expected newline after statement");
				RecoverStatement();
			}
			return stmt;
		}

		public AstStatement ParseStatementHelper()
		{
			SkipNewlines();
			var token = PeekToken();
			switch (token.Type)
			{
				case TokenType.EOF:
					return null;

					// TODO: ...
				//case TokenType.KwReturn:
				//	return ParseReturnStatement();
				//case TokenType.KwWhile:
				//	return ParseWhileStatement();
				//case TokenType.KwFor:
				//	return ParseForStatement();
				//case TokenType.KwIf:
				//	return ParseConditionalStatement();
				//case TokenType.KwContinue:
				//case TokenType.KwBreak:
				//	return ParseBcStatement();

				case TokenType.OpenBrace:
					return ParseBlockStatement();
				
				case TokenType.KwUsing:
				case TokenType.KwAttach:
					return ParseUsingStatement();

				default:
					{
						var stmt = ParseExpression(true); // anyway it should return AstStatement, not AstExpression
						if (stmt is AstEmptyStmt)
						{
							NextToken();
							return stmt;
						}
						if (CheckTokens(TokenType.Equal, TokenType.AddEq, TokenType.SubEq, TokenType.MulEq, TokenType.DivEq, TokenType.ModEq))
						{
							var x = NextToken().Type;
							string op = null;
							switch (x)
							{
								case TokenType.AddEq: op = "+"; break;
								case TokenType.SubEq: op = "-"; break;
								case TokenType.MulEq: op = "*"; break;
								case TokenType.DivEq: op = "/"; break;
								case TokenType.ModEq: op = "%"; break;
							}
							SkipNewlines();
							// TODO: do i need it???
							//var val = ParseExpression(true);
							//return new AstAssignment(expr, val, op, new Location(expr.Beginning, val.End));
						}
						else
						{
							return stmt;
						}
						return stmt;
					}
			}
		}
	}
}
