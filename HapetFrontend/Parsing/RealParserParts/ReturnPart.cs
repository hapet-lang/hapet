using HapetFrontend.Ast;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        internal AstStatement ParseReturnStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            TokenLocation beg = null;

            beg = Consume(inInfo, TokenType.KwReturn, ErrMsg("keyword 'return'", "at beginning of 'return' statement")).Location;
            SkipNewlines(inInfo);

            // if it is a simple return without params
            if (CheckToken(inInfo, TokenType.Semicolon))
            {
                return new AstReturnStmt(null, new Location(beg));
            }

            var savedMessage = inInfo.Message;
            inInfo.Message = ErrMsg("expression", "after keyword 'return'");
            var expr = ParseExpression(inInfo, ref outInfo);
            inInfo.Message = savedMessage;

            // here is the check for AstEmptyStmt because ParseExpression
            // will already generate an exception for this and return AstEmptyStmt
            // so there is no need to generate exception twice :)
            if (expr is not AstExpression && expr is not AstEmptyStmt)
            {
                ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.ExprExpectedAfterReturn));
                return ParseEmptyExpression(inInfo);
            }

            return new AstReturnStmt(expr as AstExpression, new Location(beg));
        }
    }
}
