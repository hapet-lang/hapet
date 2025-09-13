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
                beg ??= Consume(inInfo, TokenType.KwNew, ErrMsg("keyword 'new'", "at beginning of type instancing expression")).Location;
            else
                beg ??= Consume(inInfo, TokenType.KwStackAlloc, ErrMsg("keyword 'stackalloc'", "at beginning of array instancing expression")).Location;
            SkipNewlines(inInfo);

            // new expr could be unsafe
            if (CheckToken(inInfo, TokenType.KwUnsafe))
            {
                NextToken(inInfo);
                SkipNewlines(inInfo);
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
            if (CheckToken(inInfo, TokenType.OpenBracket) || CheckToken(inInfo, TokenType.ArrayDef)) // array creation
            {
                if (type is not AstExpression expr)
                {
                    ReportMessage(type.Location, [], ErrorCode.Get(CTEN.TypeNameNotExpr));
                    return ParseEmptyExpression(inInfo);
                }
                return ParseArrayExpr(inInfo, ref outInfo, expr, beg, isUnsafeNew, isStackAlloc);
            }
            else if (CheckToken(inInfo, TokenType.OpenParen)) // probably class instance creation
            {
                if (type is not AstNestedExpr nestExpr)
                {
                    ReportMessage(type.Location, [], ErrorCode.Get(CTEN.TypeNameUnexpected));
                    return ParseEmptyExpression(inInfo);
                }
                var args = ParseArgumentList(inInfo, ref outInfo, out var _, out var end);
                return new AstNewExpr(nestExpr, args, new Location(beg, end)) { IsUnsafeNew = isUnsafeNew };
            }

            // error here that unexpected token .. after typeName
            ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.TypeNameUnexpectedAfter));
            return ParseEmptyExpression(inInfo);
        }
    }
}
