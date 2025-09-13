using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        internal AstStatement ParseThrowStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            TokenLocation beg = null;

            beg = Consume(inInfo, TokenType.KwThrow, ErrMsg("keyword 'throw'", "at beginning of 'throw' statement")).Location;
            SkipNewlines(inInfo);

            var savedMessage = inInfo.Message;
            inInfo.Message = ErrMsg("'new' expression", "after keyword 'throw'");
            var expr = ParseExpression(inInfo, ref outInfo);
            inInfo.Message = savedMessage;

            // here is the check for AstEmptyStmt because ParseExpression
            // will already generate an exception for this and return AstEmptyStmt
            // so there is no need to generate exception twice :)
            if ((expr is not AstExpression && expr is not AstEmptyStmt))
            {
                ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.NewExprExpectedAfterThrow));
                return ParseEmptyExpression(inInfo);
            }

            return new AstThrowStmt(expr as AstExpression, new Location(beg));
        }
    }
}
