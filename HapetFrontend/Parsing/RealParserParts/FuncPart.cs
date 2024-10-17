using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Parsing
{
    public partial class Parser
	{
		private AstFuncDecl ParseFuncDeclaration(List<AstParamDecl> parameters, Location paramsLocation)
		{
			if (parameters == null)
			{
				parameters = ParseParameterList(TokenType.OpenParen, TokenType.CloseParen, out var beg, out var end, true);
				paramsLocation = new Location(beg, end);
			}

			AstBlockExpr body = null;

			if (CheckToken(TokenType.Semicolon))
				NextToken(); // do nothing
			else
				body = ParseBlockExpression();
			return new AstFuncDecl(parameters, null, body, null, Location: new Location(paramsLocation.Beginning, body?.Ending ?? paramsLocation.Ending));
		}

		private AstLambdaDecl ParseLambdaDeclaration(List<AstParamDecl> parameters, TokenLocation beg, bool allowCommaForTuple)
		{
			ConsumeUntil(TokenType.Arrow, ErrMsg("=>", "in lambda"));

			AstBlockExpr body = ParseBlockExpression();

			return new AstLambdaDecl(parameters, body, null, new Location(beg, body.Ending));
		}
	}
}
