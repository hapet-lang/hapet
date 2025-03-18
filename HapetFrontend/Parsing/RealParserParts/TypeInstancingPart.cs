using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseNewExpression()
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            TokenLocation beg = null;

            beg ??= Consume(TokenType.KwNew, ErrMsg("keyword 'new'", "at beginning of type instancing expression")).Location;
            SkipNewlines();

            // do not allow array expressions after 'new' word!!! but allow pointers
            inInfo.AllowPointerExpression = true;
            inInfo.AllowGeneric = true;
            inInfo.Message = ErrMsg("expression", "after keyword 'new'");
            var type = ParseAtomicExpression(inInfo, ref outInfo);

            // TokenType.ArrayDef is for array creation with ini values
            if (CheckToken(TokenType.OpenBracket) || CheckToken(TokenType.ArrayDef)) // array creation
            {
                if (type is not AstExpression expr)
                {
                    ReportMessage(type.Location, [], ErrorCode.Get(CTEN.TypeNameNotExpr));
                    return ParseEmptyExpression();
                }
                return ParseArrayExpr(expr, beg);
            }
            else if (CheckToken(TokenType.OpenParen)) // probably class instance creation
            {
                if (type is not AstNestedExpr nestExpr)
                {
                    ReportMessage(type.Location, [], ErrorCode.Get(CTEN.TypeNameUnexpected));
                    return ParseEmptyExpression();
                }
                var args = ParseArgumentList(out var _, out var end);
                return new AstNewExpr(nestExpr, args, new Location(beg, end));
            }

            // error here that unexpected token .. after typeName
            ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.TypeNameUnexpectedAfter));
            return ParseEmptyExpression();
        }
    }
}
