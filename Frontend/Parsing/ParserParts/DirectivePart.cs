using Frontend.Ast;
using Frontend.Ast.Statements;
using Frontend.Parsing.Entities;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		private AstDirectiveStmt ParseDirectiveStatement()
		{
			var dir = ParseDirective();
			return new AstDirectiveStmt(dir, dir.Location);
		}

		private List<AstDirective> ParseDirectives(bool skipNewLines = false)
		{
			var result = new List<AstDirective>();

			while (CheckToken(TokenType.SharpIdentifier))
			{
				result.Add(ParseDirective());
				if (skipNewLines)
					SkipNewlines();
			}

			return result;
		}

		private AstDirective ParseDirective()
		{
			TokenLocation end = null;
			var args = new List<AstExpression>();

			var name = ParseIdentifierExpr(ErrMsg("identifier", "after # in directive"), TokenType.SharpIdentifier);

			end = name.Ending;

			if (CheckToken(TokenType.OpenParen))
			{
				NextToken();
				SkipNewlines();

				while (true)
				{
					var next = PeekToken();
					if (next.Type == TokenType.CloseParen || next.Type == TokenType.EOF)
						break;

					var expr = ParseExpression(false);
					args.Add(expr);
					SkipNewlines();

					next = PeekToken();

					if (next.Type == TokenType.Comma)
					{
						NextToken();
						SkipNewlines();
						continue;
					}

					break;
				}

				end = Consume(TokenType.CloseParen, ErrMsg(")", "at end of directive")).Location;
			}

			return new AstDirective(name, args, new Location(name.Beginning, end));
		}
	}
}
