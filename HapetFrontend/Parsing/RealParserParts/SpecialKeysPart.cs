using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseAccessKeys(TokenType tknType)
        {
            return ParseKeysInternal(tknType);
        }

        private AstStatement ParseSyncKeys(TokenType tknType)
        {
            return ParseKeysInternal(tknType);
        }

        private AstStatement ParseInstancingKeys(TokenType tknType)
        {
            return ParseKeysInternal(tknType);
        }

        private AstStatement ParseImplementationKeys(TokenType tknType)
        {
            return ParseKeysInternal(tknType);
        }

        // they are all the same
        private AstStatement ParseKeysInternal(TokenType tknType)
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            TokenLocation beg = null;
            var tkn = Consume(tknType, ErrMsg($"keyword '{tknType}'", "at beginning of type"));
            beg = tkn.Location;

            inInfo.AllowCommaForTuple = true;
            inInfo.AllowFunctionDeclaration = true;
            inInfo.AllowPointerExpression = true;
            inInfo.AllowGeneric = true;
            var expr = ParseExpression(inInfo, ref outInfo);
            inInfo.AllowCommaForTuple = false;
            inInfo.AllowFunctionDeclaration = false;
            inInfo.AllowPointerExpression = false;
            inInfo.AllowGeneric = false;

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
            (expr as AstDeclaration).SpecialKeys.Add(tknType);

            // change beginning
            var prevLoc = expr.Location;
            expr.Location = new Location(beg, prevLoc.Ending);

            return expr;
        }
    }
}
