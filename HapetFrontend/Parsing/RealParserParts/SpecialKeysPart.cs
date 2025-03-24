using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseAccessKeys(TokenType tknType, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseKeysInternal(tknType, inInfo, ref outInfo);
        }

        private AstStatement ParseSyncKeys(TokenType tknType, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseKeysInternal(tknType, inInfo, ref outInfo);
        }

        private AstStatement ParseInstancingKeys(TokenType tknType, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseKeysInternal(tknType, inInfo, ref outInfo);
        }

        private AstStatement ParseImplementationKeys(TokenType tknType, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseKeysInternal(tknType, inInfo, ref outInfo);
        }

        // they are all the same
        private AstStatement ParseKeysInternal(TokenType tknType, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            // this var would handle 'new' token in special keys. ok?
            Token newToken = null;

            TokenLocation beg = null;
            var tkn = Consume(tknType, ErrMsg($"keyword '{tknType}'", "at beginning of type"));
            beg = tkn.Location;

            // just eat it :)
            if (CheckToken(TokenType.KwNew))
                newToken = NextToken();

            var saved1 = inInfo.AllowCommaForTuple;
            var saved2 = inInfo.AllowFunctionDeclaration;
            var saved3 = inInfo.AllowPointerExpression;
            var saved4 = inInfo.AllowGeneric;

            inInfo.AllowCommaForTuple = true;
            inInfo.AllowFunctionDeclaration = true;
            inInfo.AllowPointerExpression = true;
            inInfo.AllowGeneric = true;
            var expr = ParseExpression(inInfo, ref outInfo);
            inInfo.AllowCommaForTuple = saved1;
            inInfo.AllowFunctionDeclaration = saved2;
            inInfo.AllowPointerExpression = saved3;
            inInfo.AllowGeneric = saved4;

            // it could be an idexpr or nestedexpr when ctor/dtor decls are here
            if (expr is AstIdExpr idExpr)
            {
                expr = new AstUnknownDecl(null, idExpr, expr);
            }
            else if (expr is AstNestedExpr nestExpr && nestExpr.RightPart is AstIdExpr idExpr2 && nestExpr.LeftPart == null)
            {
                expr = new AstUnknownDecl(null, idExpr2, expr);
            }
            // probably only for overloading
            else if (expr is AstNestedExpr nestExpr2)
            {
                expr = new AstUnknownDecl(nestExpr2, null, expr);
            }

            // because it has to be declaration
            if (expr is not AstDeclaration)
            {
                ReportMessage(expr.Location, [tknType.ToString()], ErrorCode.Get(CTEN.DeclExpectedAfterTheToken));
                return ParseEmptyExpression();
            }

            if (newToken != null)
                (expr as AstDeclaration).SpecialKeys.Insert(0, newToken.Type);
            (expr as AstDeclaration).SpecialKeys.Insert(0, tknType);

            // change beginning
            var prevLoc = expr.Location;
            expr.Location = new Location(beg, prevLoc.Ending);

            return expr;
        }
    }
}
