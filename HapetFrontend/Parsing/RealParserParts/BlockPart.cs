using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;

namespace HapetFrontend.Parsing
{
    public partial class Parser
	{
		private AstBlockExpr ParseBlockExpression()
		{
			var statements = new List<AstStatement>();
			var beg = Consume(TokenType.OpenBrace, ErrMsg("{", "at beginning of block expression")).Location;

			// the string is used to check if BR found in the block
			// so do not accept any statements after it
			string foundBrStatement = string.Empty;

			SkipNewlines();
			while (true)
			{
				var next = PeekToken();
				if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
					break;

				var s = ParseStatement(false);
				if (s != null)
				{
					if (string.IsNullOrWhiteSpace(foundBrStatement))
					{
						statements.Add(s);
					}
					else
					{
						// TODO: print warning that the line won't be accepted
						// TODO: print the warning only once, do not spam
					}

					next = PeekToken();

					if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
						break;

					switch (s)
					{
						case AstReturnStmt:
							foundBrStatement = "return";
							break;
						case AstBreakContStmt bc:
							foundBrStatement = bc.IsBreak ? "break" : "continue";
							break;

						default:
							// TODO: do i really need this shite?
							//if (!Expect(TokenType.NewLine, ErrMsg("\\n", "after statement")))
							//	RecoverStatement();
							break;
					}

				}
				SkipNewlines();
			}

			var end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of block expression")).Location;

			return new AstBlockExpr(statements, new Location(beg, end));
		}
	}
}
