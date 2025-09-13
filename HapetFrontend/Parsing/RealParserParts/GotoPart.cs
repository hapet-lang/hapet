using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        internal AstStatement ParseGotoStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            TokenLocation beg = null;

            beg = Consume(inInfo, TokenType.KwGoto, ErrMsg("keyword 'goto'", "at beginning of 'goto' statement")).Location;
            SkipNewlines(inInfo);

            var expr = ParseExpression(inInfo, ref outInfo);
            if (!(expr is AstNestedExpr nst && nst.RightPart is AstIdExpr idExpr))
            {
                // error here
                ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                return new AstEmptyStmt();
            }

            return new AstGotoStmt(idExpr.Name, new Location(beg, idExpr.Ending));
        }
    }
}
