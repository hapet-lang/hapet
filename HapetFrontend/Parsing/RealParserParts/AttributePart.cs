using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using System.Runtime;
using System.Xml.Linq;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseAttributeStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            TokenLocation beg = null;
            TokenLocation end = null;
            AstNestedExpr attrName = null;
            List<AstArgumentExpr> args = new List<AstArgumentExpr>();

            beg = Consume(TokenType.OpenBracket, ErrMsg("token '['", "at beginning of attribute statement")).Location;
            SkipNewlines();

            // attr name
            if (!CheckToken(TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.AttrNameExpected));
            }
            else
            {
                attrName = ParseIdentifierExpression(inInfo, allowDots: true);
            }

            // parsing attr args
            if (CheckToken(TokenType.OpenParen))
            {
                args = ParseArgumentList(inInfo, ref outInfo, out var _, out var _);
            }

            end = Consume(TokenType.CloseBracket, ErrMsg("token ']'", "at the end of attribute statement")).Location;

            return new AstAttributeStmt(attrName, args, new Location(beg, end));
        }
    }
}
