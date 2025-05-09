using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;

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
                SkipNewlines();
            }

            AstBlockExpr body = null;
            AstBaseCtorStmt baseCtorCall = null;
            List<AstIdExpr> generics = new List<AstIdExpr>();

            // getting generics from parsed udecl' name
            if (inInfo.CurrentUdecl != null && inInfo.CurrentUdecl.Name is AstIdGenericExpr genExpr)
            {
                generics = GenericsHelper.GetGenericsFromName(genExpr, _messageHandler);
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

            var theFunc = new AstFuncDecl(parameters, null, null, null);

            // allow nested func decls
            inInfo.AllowNestedFunc = true;
            inInfo.ParentFuncDecl = theFunc;
            if (CheckToken(TokenType.Semicolon))
                NextToken(); // do nothing
            else
                body = ParseBlockExpression(inInfo, ref outInfo);
            inInfo.AllowNestedFunc = false;
            inInfo.ParentFuncDecl = null;


            theFunc.Body = body;
            theFunc.Location = new Location(paramsLocation.Beginning, body?.Ending ?? paramsLocation.Ending);
            theFunc.BaseCtorCall = baseCtorCall;
            theFunc.HasGenericTypes = generics.Count > 0;
            theFunc.GenericConstrains = genericConstrains;
            theFunc.IsImported = inInfo.ExternalMetadata;
            return theFunc;
        }

        private AstLambdaDecl ParseLambdaDeclaration(List<AstParamDecl> parameters, TokenLocation beg, bool allowCommaForTuple)
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            ConsumeUntil(TokenType.Arrow, ErrMsg("=>", "in lambda"));

            AstBlockExpr body = ParseBlockExpression(inInfo, ref outInfo);

            return new AstLambdaDecl(parameters, body, null, new Location(beg, body.Ending));
        }
    }
}
