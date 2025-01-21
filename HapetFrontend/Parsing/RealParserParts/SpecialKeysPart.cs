using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
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
            TokenLocation beg = null;
            var tkn = Consume(tknType, ErrMsg($"keyword '{tknType}'", "at beginning of type"));
            beg = tkn.Location;

            var expr = ParseExpression(true, true, null, true);

            // it could be an idexpr or nestedexpr when ctor/dtor decls are here
            if (expr is AstIdExpr idExpr)
            {
                expr = new UnknownDecl(null, idExpr, expr);
            }
            else if (expr is AstNestedExpr nestExpr && nestExpr.RightPart is AstIdExpr idExpr2 && nestExpr.LeftPart == null)
            {
                expr = new UnknownDecl(null, idExpr2, expr);
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
