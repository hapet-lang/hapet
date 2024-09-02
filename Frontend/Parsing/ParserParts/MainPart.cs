using Frontend.Ast.Statements;
using Frontend.Ast;
using Frontend.Parsing.Entities;
using Frontend.Ast.Expressions;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		public AstStatement ParseStatement(bool expectNewline = true)
		{
			var stmt = ParseStatementHelper();

			if (stmt == null)
				return null;

			// TODO: probably no need
			//if (CheckToken(TokenType.Semicolon))
			//{
			//	var stmts = new List<AstStatement> { stmt };
			//	while (CheckToken(TokenType.Semicolon))
			//	{
			//		NextToken();
			//		SkipNewlines();
			//		var s = ParseStatementHelper();
			//		if (s == null)
			//			break;

			//		stmts.Add(s);
			//	}

			//	var location = new Location(stmts.First().Beginning, stmts.Last().End);

			//	// @temporary, these statements should not create a new scope
			//	var block = new AstBlockExpr(stmts, Location: location);
			//	block.SetFlag(ExprFlags.Anonymous, true);
			//	stmt = new AstExprStmt(block, location);
			//}

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

					// TODO:
				//case TokenType.SharpIdentifier:
				//	return ParseDirectiveStatement();

				//case TokenType.KwReturn:
				//	return ParseReturnStatement();
				//case TokenType.KwWhile:
				//	return ParseWhileStatement();
				//case TokenType.KwLoop:
				//	return ParseLoopStatement();
				//case TokenType.KwFor:
				//	return ParseForStatement();
				//case TokenType.KwImpl:
				//	return ParseImplBlock();
				case TokenType.OpenBrace:
					return ParseBlockStatement();

				case TokenType.KwUsing:
					return ParseUsingStatement();

				case TokenType.KwAttach:
					return ParseAttachStatement();

				default:
					{
						var expr = ParseExpression(true);
						if (expr is AstEmptyExpr)
						{
							NextToken();
							return new AstEmptyStatement(expr.Location);
						}
						//if (CheckToken(TokenType.Colon))
						//{
						//	var decl = ParseDeclaration(expr, false, true, null, true);
						//	return decl;
						//}
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
							var val = ParseExpression(true);
							return new AstAssignment(expr, val, op, new Location(expr.Beginning, val.End));
						}
						else
						{
							return new AstExprStmt(expr, new Location(expr.Beginning, expr.End));
						}
					}
			}
		}
	}
}
