using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;

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
            AstBaseCtorStmt baseCtorCall = null;

            SkipNewlines();

            // check for base ctor call
            if (CheckToken(TokenType.Colon))
            {
                NextToken();
                SkipNewlines();
                var bsTkn = Consume(TokenType.KwBase, ErrMsg("'base'", "after ':'"));
                var args = ParseArgumentList(out var end);
                baseCtorCall = new AstBaseCtorStmt(args, new Location(bsTkn.Location, end));
            }

            SkipNewlines();

            if (CheckToken(TokenType.Semicolon))
                NextToken(); // do nothing
            else
                body = ParseBlockExpression();
            return new AstFuncDecl(parameters, null, body, null, location: new Location(paramsLocation.Beginning, body?.Ending ?? paramsLocation.Ending)) { BaseCtorCall = baseCtorCall };
        }

        private AstLambdaDecl ParseLambdaDeclaration(List<AstParamDecl> parameters, TokenLocation beg, bool allowCommaForTuple)
        {
            ConsumeUntil(TokenType.Arrow, ErrMsg("=>", "in lambda"));

            AstBlockExpr body = ParseBlockExpression();

            return new AstLambdaDecl(parameters, body, null, new Location(beg, body.Ending));
        }
    }
}
