using Frontend.Ast;
using Frontend.Parsing.Entities;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		private AstExpression ParseAccessKeys(TokenType tknType)
		{
			TokenLocation beg = null;
			var tkn = Consume(tknType, ErrMsg($"keyword '{tknType}'", "at beginning of class type"));
			beg = tkn.Location;

			var expr = ParseExpression(false);
			expr.SpecialKeys.Add((int)tknType);

			// change beginning
			var prevLoc = expr.Location;
			expr.Location = new Location(beg, prevLoc.Ending);

			return expr;
		}
	}
}
