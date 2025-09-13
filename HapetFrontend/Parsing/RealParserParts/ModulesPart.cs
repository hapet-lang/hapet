using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using System.Collections.Generic;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseUsingStatement(ParserInInfo inInfo)
        {
            TokenLocation beg = null;

            beg ??= Consume(inInfo, TokenType.KwUsing, ErrMsg("keyword 'using'", "at beginning of 'using' statement")).Location;
            SkipNewlines(inInfo);

            var savedM = inInfo.Message;
            inInfo.Message = ErrMsg("expression", "after keyword 'using'");
            var expr = ParseIdentifierExpression(inInfo);
            inInfo.Message = savedM;

            if (expr is not AstNestedExpr)
            {
                ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.NoNamespaceAfterUsing));
                return ParseEmptyExpression(inInfo);
            }

            return new AstUsingStmt(expr, new Location(beg));
        }

        private AstStatement ParseNamespaceStatement(ParserInInfo inInfo)
        {
            TokenLocation beg = null;

            beg ??= Consume(inInfo, TokenType.KwNamespace, ErrMsg("keyword 'namespace'", "at beginning of 'namespace' statement")).Location;
            SkipNewlines(inInfo);

            var savedM = inInfo.Message;
            inInfo.Message = ErrMsg("expression", "after keyword 'namespace'");
            var expr = ParseIdentifierExpression(inInfo);
            inInfo.Message = savedM;

            if (expr is not AstNestedExpr)
            {
                ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.NoNamespaceAfterNamespace));
                return ParseEmptyExpression(inInfo);
            }

            return new AstNamespaceStmt(expr, new Location(beg));
        }
    }
}
