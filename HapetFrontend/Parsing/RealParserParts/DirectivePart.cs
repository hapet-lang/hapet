using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseDirectiveStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var tkn = Consume(TokenType.SharpIdentifier, ErrMsg("char '#'", "at beginning of directive"));
            DirectiveType type = DirectiveType.None;

            switch ((string)tkn.Data)
            {
                case "file":
                    type = DirectiveType.MetadataFile;
                    break;
            }

            switch (type) 
            {
                case DirectiveType.MetadataFile:
                    {
                        var expr = ParseExpression(inInfo, ref outInfo);
                        if (expr is not AstStringExpr)
                        {
                            // error here
                            ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.CommonStringExpected));
                        }
                        return new AstDirectiveStmt(expr, type, new Location(tkn.Location, expr.Location.Ending));
                    }
            }

            // error here
            ReportMessage(tkn.Location, [], ErrorCode.Get(CTEN.UnexpectedDirective));
            return new AstEmptyStmt();
        }
    }
}
