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

			var args = ParseArgumentList(out var _);
			return new AstNewExpr(typeName, args, Location: new Location(beg));
		}
	}
}
