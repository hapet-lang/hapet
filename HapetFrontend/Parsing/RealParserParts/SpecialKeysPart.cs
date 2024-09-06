using HapetFrontend.Ast;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstStatement ParseAccessKeys(TokenType tknType)
		{
			return ParseKeysInternal(tknType);
		}

		private AstStatement ParseSyncKeys(TokenType tknType)
		{
			return ParseKeysInternal(tknType);
		}

		private AstStatement ParseInstancingKeys(TokenType tknType)
		{
			return ParseKeysInternal(tknType);
		}

		private AstStatement ParseImplementationKeys(TokenType tknType)
		{
			return ParseKeysInternal(tknType);
		}

		// they are all the same
		private AstStatement ParseKeysInternal(TokenType tknType)
		{
			TokenLocation beg = null;
			var tkn = Consume(tknType, ErrMsg($"keyword '{tknType}'", "at beginning of type"));
			beg = tkn.Location;

			var expr = ParseExpression(false);
			// because it has to be declaration
			if (expr is not AstDeclaration)
			{
				ReportError(expr.Location, $"The statement after {tknType} has to be a declaration");
				return ParseEmptyExpression();
			}
			(expr as AstDeclaration).SpecialKeys.Add(tknType);

			// change beginning
			var prevLoc = expr.Location;
			expr.Location = new Location(beg, prevLoc.Ending);

			return expr;
		}
	}
}
