using HapetFrontend.Ast;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        public AstStatement ParseReturnStatement()
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            TokenLocation beg = null;

            beg = Consume(TokenType.KwReturn, ErrMsg("keyword 'return'", "at beginning of 'return' statement")).Location;
            SkipNewlines();

            // if it is a simple return without params
            if (CheckToken(TokenType.Semicolon))
            {
                return new AstReturnStmt(null, new Location(beg));
            }

            inInfo.Message = ErrMsg("expression", "after keyword 'return'");
            var expr = ParseExpression(inInfo, ref outInfo);

            // here is the check for AstEmptyStmt because ParseExpression
            // will already generate an exception for this and return AstEmptyStmt
            // so there is no need to generate exception twice :)
            if (expr is not AstExpression && expr is not AstEmptyStmt)
            {
                ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.ExprExpectedAfterReturn));
                return ParseEmptyExpression();
            }

            return new AstReturnStmt(expr as AstExpression, new Location(beg));
        }
    }
}
