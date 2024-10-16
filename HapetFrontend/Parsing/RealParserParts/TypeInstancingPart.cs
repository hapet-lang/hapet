using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstStatement ParseNewExpression()
		{
			TokenLocation beg = null;

			beg ??= Consume(TokenType.KwNew, ErrMsg("keyword 'new'", "at beginning of type instancing expression")).Location;
			SkipNewlines();
			var typeName = ParseIdentifierExpression(ErrMsg("expression", "after keyword 'new'"));

			if (CheckToken(TokenType.OpenBracket)) // array creation
			{
				return ParseArrayExpr(typeName, beg);
			}
			else if (CheckToken(TokenType.OpenParen)) // probably class instance creation
			{
				var args = ParseArgumentList(out var _);
				return new AstNewExpr(typeName, args, Location: new Location(beg));
			}

			// error here that unexpected token .. after typeName
			ReportError(PeekToken().Location, $"Unexpected token after a type name");
			return ParseEmptyExpression();
		}
	}
}
