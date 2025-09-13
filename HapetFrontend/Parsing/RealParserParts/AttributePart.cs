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

            beg = Consume(inInfo, TokenType.OpenBracket, ErrMsg("token '['", "at beginning of attribute statement")).Location;
            SkipNewlines(inInfo);

            // attr name
            if (!CheckToken(inInfo, TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.AttrNameExpected));
            }
            else
            {
                attrName = ParseIdentifierExpression(inInfo, allowDots: true);
            }

            // parsing attr args
            if (CheckToken(inInfo, TokenType.OpenParen))
            {
                args = ParseArgumentList(inInfo, ref outInfo, out var _, out var _);
            }

            end = Consume(inInfo, TokenType.CloseBracket, ErrMsg("token ']'", "at the end of attribute statement")).Location;

            return new AstAttributeStmt(attrName, args, new Location(beg, end));
        }
    }
}
