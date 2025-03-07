using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstFuncDecl ParseFuncDeclaration(List<AstParamDecl> parameters, Location paramsLocation, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            if (parameters == null)
            {
                parameters = ParseParameterList(TokenType.OpenParen, TokenType.CloseParen, out var beg, out var end, true);
                paramsLocation = new Location(beg, end);
            }

            AstBlockExpr body = null;
            AstBaseCtorStmt baseCtorCall = null;
            List<AstIdExpr> generics = new List<AstIdExpr>();

            // getting generics from parsed udecl' name
            if (inInfo.CurrentUdecl != null && inInfo.CurrentUdecl.Name is AstIdGenericExpr genExpr)
            {
                generics = genExpr.GenericRealTypes.Select(x => x.RightPart as AstIdExpr).ToList();
            }

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

            // parsing constrains
            var genericConstrains = ParseGenericConstrains(generics);

            SkipNewlines();

            if (CheckToken(TokenType.Semicolon))
                NextToken(); // do nothing
            else
                body = ParseBlockExpression();
            return new AstFuncDecl(parameters, null, body, null, location: new Location(paramsLocation.Beginning, body?.Ending ?? paramsLocation.Ending)) 
            { 
                BaseCtorCall = baseCtorCall,
                HasGenericTypes = generics.Count > 0,
                GenericNames = generics,
                GenericConstrains = genericConstrains,
            };
        }

        private AstLambdaDecl ParseLambdaDeclaration(List<AstParamDecl> parameters, TokenLocation beg, bool allowCommaForTuple)
        {
            ConsumeUntil(TokenType.Arrow, ErrMsg("=>", "in lambda"));

            AstBlockExpr body = ParseBlockExpression();

            return new AstLambdaDecl(parameters, body, null, new Location(beg, body.Ending));
        }
    }
}
