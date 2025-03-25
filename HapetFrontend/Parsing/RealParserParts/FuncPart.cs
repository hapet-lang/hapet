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
            // disable new as sk allowance!!!
            inInfo.AllowNewAsSpecialKey = false;

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
                foreach (var g in genExpr.GenericRealTypes)
                {
                    if (g is AstNestedExpr nest)
                        generics.Add(nest.RightPart as AstIdExpr);
                    else if (g is AstIdExpr id)
                        generics.Add(id);
                    else
                        generics.Add(null); // TODO: ERROR HERE
                }
            }

            SkipNewlines();

            // check for base ctor call
            if (CheckToken(TokenType.Colon))
            {
                NextToken();
                SkipNewlines();
                var bsTkn = Consume(TokenType.KwBase, ErrMsg("'base'", "after ':'"));
                var args = ParseArgumentList(out var _, out var end);
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
                IsImported = inInfo.ExternalMetadata
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
