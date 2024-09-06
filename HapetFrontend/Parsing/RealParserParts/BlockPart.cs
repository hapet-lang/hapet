using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstBlockStmt ParseBlockStatement()
		{
			var statements = new List<AstStatement>();
			var beg = Consume(TokenType.OpenBrace, ErrMsg("{", "at beginning of block statement")).Location;

			SkipNewlines();
			while (true)
			{
				var next = PeekToken();
				if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
					break;

				var s = ParseStatement(false);
				if (s != null)
				{
					statements.Add(s);

					next = PeekToken();

					if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
						break;

					switch (s)
					{
						// TODO: uncomment
						// case AstConditionStmt:
						case AstBlockStmt:
							break;

						default:
							if (!Expect(TokenType.NewLine, ErrMsg("\\n", "after statement")))
								RecoverStatement();
							break;
					}

				}
				SkipNewlines();
			}

			var end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of block statement")).Location;

			return new AstBlockStmt(statements, new Location(beg, end));
		}
	}
}
