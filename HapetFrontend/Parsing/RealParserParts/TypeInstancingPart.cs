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
			// do not allow array expressions after 'new' word!!! but allow pointers
			var type = ParseAtomicExpression(false, false, ErrMsg("expression", "after keyword 'new'"), false, true);

			// TokenType.ArrayDef is for array creation with ini values
			if (CheckToken(TokenType.OpenBracket) || CheckToken(TokenType.ArrayDef)) // array creation
			{
                if (type is not AstExpression expr)
                {
                    ReportError(type.Location, $"Expression expected as a type name");
                    return ParseEmptyExpression();
                }
                return ParseArrayExpr(expr, beg);
			}
			else if (CheckToken(TokenType.OpenParen)) // probably class instance creation
			{
				if (type is not AstNestedExpr nestExpr)
				{
                    ReportError(type.Location, $"Unexpected token as a type name");
                    return ParseEmptyExpression();
                }
				var args = ParseArgumentList(out var _);
				return new AstNewExpr(nestExpr, args, Location: new Location(beg));
			}

			// error here that unexpected token .. after typeName
			ReportError(PeekToken().Location, $"Unexpected token after a type name");
			return ParseEmptyExpression();
		}
	}
}
