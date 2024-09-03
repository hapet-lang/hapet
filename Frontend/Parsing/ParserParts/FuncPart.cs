using Frontend.Ast.Expressions;
using Frontend.Ast;
using Frontend.Parsing.Entities;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		private AstFuncExpr ParseFuncExpr(List<AstParameter> parameters, Location paramsLocation)
		{
			if (parameters == null)
			{
				parameters = ParseParameterList(TokenType.OpenParen, TokenType.CloseParen, out var beg, out var end, true);
				paramsLocation = new Location(beg, end);
			}

			AstBlockExpr body = null;
			AstParameter returnType = null;

			// function decl with return type
			//if (CheckToken(TokenType.Arrow))
			//{
			//	NextToken();
			//	returnType = ParseParameter(true);
			//}

			var directives = ParseDirectives();

			if (CheckToken(TokenType.Semicolon))
				NextToken(); // do nothing
			else
				body = ParseBlockExpr();
			return new AstFuncExpr(parameters, returnType, body, directives, Location: new Location(paramsLocation.Beginning, body?.Ending ?? paramsLocation.Ending), ParameterLocation: paramsLocation);
		}

		private AstExpression ParseLambdaExpr(List<AstParameter> parameters, TokenLocation beg, bool allowCommaForTuple)
		{
			AstExpression retType = null;

			ConsumeUntil(TokenType.Arrow, ErrMsg("=>", "in lambda"));

			var body = ParseExpression(allowCommaForTuple);

			return new AstLambdaExpr(parameters, body, retType, new Location(beg, body.Ending));
		}
	}
}
