using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using System.Diagnostics;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstFuncDecl ParseFuncDeclaration(ParserInInfo inInfo, ref ParserOutInfo outInfo, List<AstParamDecl> parameters, Location paramsLocation, bool isReturnVoid)
        {
            if (parameters == null)
            {
                parameters = ParseParameterList(inInfo, ref outInfo, TokenType.OpenParen, TokenType.CloseParen, out var beg, out var end, true);
                paramsLocation = new Location(beg, end);
                SkipNewlines(inInfo);
            }

            AstBlockExpr body = null;
            AstBaseCtorStmt baseCtorCall = null;
            AstBaseCtorStmt thisCtorCall = null; // call of another ctor from one ctor 
            List<AstIdExpr> generics = new List<AstIdExpr>();

            // getting generics from parsed udecl' name
            if (inInfo.CurrentUdecl != null && inInfo.CurrentUdecl.Name is AstIdGenericExpr genExpr)
            {
                generics = GenericsHelper.GetGenericsFromName(genExpr, _messageHandler);
            }

            SkipNewlines(inInfo);

            // check for base ctor call
            if (CheckToken(inInfo, TokenType.Colon))
            {
                NextToken(inInfo);
                SkipNewlines(inInfo);
                if (CheckToken(inInfo, TokenType.KwBase))
                {
                    var bsTkn = Consume(inInfo, TokenType.KwBase, ErrMsg("'base'", "after ':'"));
                    var args = ParseArgumentList(inInfo, ref outInfo, out var _, out var end);
                    baseCtorCall = new AstBaseCtorStmt(args, new Location(bsTkn.Location, end));
                }
                else
                {
                    var thiss = ParseIdentifierExpression(inInfo, allowDots: false, allowGenerics: false, expectIdent: true);
                    if (thiss.RightPart is not AstIdExpr idExpr || idExpr.Name != "this")
                    {
                        ReportMessage(thiss.Location, [], ErrorCode.Get(CTEN.PureUnexpectedToken));
                        return null;
                    }
                    var args = ParseArgumentList(inInfo, ref outInfo, out var _, out var end);
                    thisCtorCall = new AstBaseCtorStmt(args, new Location(thiss.Location.Beginning, end));
                    thisCtorCall.IsThisCtorCall = true;
                }
            }

            // parsing constrains
            var genericConstrains = ParseGenericConstrains(generics);

            SkipNewlines(inInfo);

            var theFunc = new AstFuncDecl(parameters, null, null, null);

            // allow nested func decls
            inInfo.AllowNestedFunc = true;
            inInfo.ParentFuncDecl = theFunc;
            if (CheckToken(inInfo, TokenType.Semicolon))
                NextToken(inInfo); // do nothing
            else if (CheckToken(inInfo, TokenType.Arrow))
                body = ParseFunctionArrow(inInfo, ref outInfo, isReturnVoid);
            else
                body = ParseBlockExpression(inInfo, ref outInfo);
            inInfo.AllowNestedFunc = false;
            inInfo.ParentFuncDecl = null;

            theFunc.Body = body;
            theFunc.Location = new Location(paramsLocation.Beginning, body?.Ending ?? paramsLocation.Ending);
            theFunc.BaseCtorCall = baseCtorCall;
            theFunc.ThisCtorCall = thisCtorCall;
            theFunc.HasGenericTypes = generics.Count > 0;
            theFunc.GenericConstrains = genericConstrains;
            theFunc.IsImported = inInfo.ExternalMetadata;
            return theFunc;
        }

        private AstBlockExpr ParseFunctionArrow(ParserInInfo inInfo, ref ParserOutInfo outInfo, bool isReturnVoid)
        {
            NextToken(inInfo);

            // getting only one stmt if there are no braces
            var onlyStmt = ParseStatement(inInfo, ref outInfo);

            // if not void type - add return
            if (!isReturnVoid)
            {
                var retStmt = onlyStmt as AstExpression;
                Debug.Assert(retStmt != null);
                onlyStmt = new AstReturnStmt(retStmt, onlyStmt.Location);
            }

            var body = new AstBlockExpr(new List<AstStatement>() { onlyStmt }, onlyStmt);

            // try eat semicolon or error
            CheckSemicolonAfterStmt(inInfo, onlyStmt);
            return body;
        }
    }
}
