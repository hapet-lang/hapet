using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseNewExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo, bool isStackAlloc)
        {
            TokenLocation beg = null;
            bool isUnsafeNew = false;

            if (!isStackAlloc)
                beg ??= Consume(TokenType.KwNew, ErrMsg("keyword 'new'", "at beginning of type instancing expression")).Location;
            else
                beg ??= Consume(TokenType.KwStackAlloc, ErrMsg("keyword 'stackalloc'", "at beginning of array instancing expression")).Location;
            SkipNewlines();

            // new expr could be unsafe
            if (CheckToken(TokenType.KwUnsafe))
            {
                NextToken();
                SkipNewlines();
                isUnsafeNew = true;
            }

            // do not allow array expressions after 'new' word!!! but allow pointers
            var saved = inInfo.AllowArrayExpression;
            var savedMessage = inInfo.Message;
            inInfo.AllowArrayExpression = false;
            inInfo.Message = isStackAlloc ? ErrMsg("expression", "after keyword 'stackalloc'") : ErrMsg("expression", "after keyword 'new'");
            var type = ParseAtomicExpression(inInfo, ref outInfo);
            inInfo.AllowArrayExpression = saved;
            inInfo.Message = savedMessage;

            // TokenType.ArrayDef is for array creation with ini values
            if (CheckToken(TokenType.OpenBracket) || CheckToken(TokenType.ArrayDef)) // array creation
            {
                if (type is not AstExpression expr)
                {
                    ReportMessage(type.Location, [], ErrorCode.Get(CTEN.TypeNameNotExpr));
                    return ParseEmptyExpression();
                }
                return ParseArrayExpr(inInfo, ref outInfo, expr, beg, isUnsafeNew, isStackAlloc);
            }
            else if (CheckToken(TokenType.OpenParen)) // probably class instance creation
            {
                if (type is not AstNestedExpr nestExpr)
                {
                    ReportMessage(type.Location, [], ErrorCode.Get(CTEN.TypeNameUnexpected));
                    return ParseEmptyExpression();
                }
                var args = ParseArgumentList(inInfo, ref outInfo, out var _, out var end);
                return new AstNewExpr(nestExpr, args, new Location(beg, end)) { IsUnsafeNew = isUnsafeNew };
            }

            // error here that unexpected token .. after typeName
            ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.TypeNameUnexpectedAfter));
            return ParseEmptyExpression();
        }
    }
}
