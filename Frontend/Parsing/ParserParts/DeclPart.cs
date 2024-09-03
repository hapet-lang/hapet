using Frontend.Ast;
using Frontend.Parsing.Entities;
using System.Diagnostics;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		private AstDeclaration ParseDeclaration(AstExpression expr, bool allowCommaTuple, List<AstDirective> directives, bool parseDirectives)
		{
			var docString = GetCurrentDocString();
			if (expr == null)
				expr = ParseExpression(allowCommaTuple);

			Consume(TokenType.Colon, ErrMsg(":", "after pattern in declaration"));

			AstExpression typeExpr = null;
			AstExpression initializer = null;
			TokenLocation end = expr.Ending;

			// TODO: check it

			//// constant declaration
			//if (!CheckTokens(TokenType.Colon, TokenType.Equal))
			//{
			//	typeExpr = ParseExpression(allowCommaTuple);
			//	end = typeExpr.Ending;
			//}

			//// constant declaration
			//if (CheckToken(TokenType.Colon))
			//{
			//	NextToken();
			//	var init = ParseExpression(allowCommaTuple, allowFunctionExpression: true);
			//	return new AstConstantDeclaration(expr, typeExpr, init, docString, directives, Location: new Location(expr.Beginning, init.End));
			//}

			//// variable declaration without initializer but with directives
			//if (parseDirectives && CheckToken(TokenType.HashIdentifier))
			//{
			//	Debug.Assert(directives == null);
			//	directives = ParseDirectives();
			//	return new AstVariableDecl(expr, typeExpr, null, mut, docString, directives, Location: new Location(expr.Beginning, directives.Last().End));
			//}

			//// variable declaration with initializer
			//if (CheckToken(TokenType.Equal))
			//{
			//	NextToken();
			//	initializer = ParseExpression(allowCommaTuple);
			//	end = initializer.End;
			//}

			//// variable declaration with initializer and with directives
			//if (parseDirectives && CheckToken(TokenType.HashIdentifier))
			//{
			//	Debug.Assert(directives == null);
			//	directives = ParseDirectives();
			//	end = directives.Last().End;
			//}

			//// variable declaration without initializer
			//if (CheckTokens(TokenType.NewLine, TokenType.EOF))
			//{
			//	return new AstVariableDecl(expr, typeExpr, initializer, mut, docString, directives, Location: new Location(expr.Beginning, end));
			//}

			////
			//ReportError(PeekToken().Location, $"Unexpected token. Expected ':' or '=' or '\\n'");
			//return new AstVariableDecl(expr, typeExpr, null, mut, docString, directives, Location: expr);
			return null;
		}
	}
}
