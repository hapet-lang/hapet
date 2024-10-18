using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
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
				Consume(TokenType.Semicolon, ErrMsg(";", "at the end of the statement"));
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

				case TokenType.KwReturn:
					return ParseReturnStatement();
				// TODO: ...
				//case TokenType.KwWhile:
				//	return ParseWhileStatement();
				case TokenType.KwFor:
					return ParseForStatement();
				//case TokenType.KwIf:
				//	return ParseConditionalStatement();
				case TokenType.KwContinue:
				case TokenType.KwBreak:
					return new AstBreakContStmt(token.Type == TokenType.KwBreak, new Location(token.Location));

				case TokenType.KwUsing:
					return ParseUsingStatement();

				case TokenType.OpenBrace:
					return ParseBlockExpression();

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
							var currT = NextToken();
							var x = currT.Type;
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

							var val = ParseExpression(true);

							if (val is not AstExpression)
							{
								ReportError(val.Location, $"The right side of variable assignment has to be an expression");
								return stmt;
							}

							if (stmt is UnknownDecl udecl)
							{
								// if it is a declaration with initializing
								if (x != TokenType.Equal)
								{
									ReportError(currT.Location, $"Variable initializer expected (=) but got {op}=");
									return stmt;
								}
								return new AstVarDecl(udecl.Type, udecl.Name, val as AstExpression, "", new Location(stmt.Beginning, val.Ending));
							}
							else if (stmt is AstNestedExpr id)
							{
								// expand ops like 'a += b' into 'a = a + b'
								var binOpExpr = new AstBinaryExpr(op, id, val, new Location(id.Location.Beginning, val.Location.Ending));
								return new AstAssignStmt(id, binOpExpr, new Location(stmt.Beginning, val.Ending));
							}
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
