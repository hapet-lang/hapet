using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Parsing
{
    public partial class Parser
	{
		private AstBlockExpr ParseBlockExpression()
		{
			var statements = new List<AstStatement>();
			var beg = Consume(TokenType.OpenBrace, ErrMsg("{", "at beginning of block expression")).Location;

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
						case AstBlockExpr:
							break;

						default:
							if (!Expect(TokenType.NewLine, ErrMsg("\\n", "after statement")))
								RecoverStatement();
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
