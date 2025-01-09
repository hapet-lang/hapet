using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using System.Xml.Linq;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseAttributeStatement()
        {
            TokenLocation beg = null;
            TokenLocation end = null;
            AstNestedExpr attrName = null;
            List<AstExpression> parameters = new List<AstExpression>();

            beg = Consume(TokenType.OpenBracket, ErrMsg("token '['", "at beginning of attribute statement")).Location;
            SkipNewlines();

            // attr name
            if (!CheckToken(TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken().Location, $"Expected attribute name");
            }
            else
            {
                attrName = ParseIdentifierExpression(allowDots: true);
            }

            // parsing attr args
            if (CheckToken(TokenType.OpenParen))
            {
                var args = ParseArgumentList(out var _);
                foreach (var a in args)
                {
                    parameters.Add(a.Expr);
                }
            }

            end = Consume(TokenType.CloseBracket, ErrMsg("token ']'", "at the end of attribute statement")).Location;

            return new AstAttributeStmt(attrName, parameters, new Location(beg, end));
        }
    }
}
