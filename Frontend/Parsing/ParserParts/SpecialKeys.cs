using Frontend.Ast;
using Frontend.Parsing.Entities;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		private AstExpression ParseAccessKeys(TokenType tknType)
		{
			return ParseKeysInternal(tknType);
		}

		private AstExpression ParseSyncKeys(TokenType tknType)
		{
			return ParseKeysInternal(tknType);
		}

		private AstExpression ParseInstancingKeys(TokenType tknType)
		{
			return ParseKeysInternal(tknType);
		}

		private AstExpression ParseImplementationKeys(TokenType tknType)
		{
			return ParseKeysInternal(tknType);
		}

		// they are all the same
		private AstExpression ParseKeysInternal(TokenType tknType)
		{
			TokenLocation beg = null;
			var tkn = Consume(tknType, ErrMsg($"keyword '{tknType}'", "at beginning of type"));
			beg = tkn.Location;

			var expr = ParseExpression(false);
			expr.SpecialKeys.Add(tknType);

			// change beginning
			var prevLoc = expr.Location;
			expr.Location = new Location(beg, prevLoc.Ending);

			return expr;
		}
	}
}
