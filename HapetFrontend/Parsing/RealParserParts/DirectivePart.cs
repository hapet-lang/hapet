using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseDirectiveStatement()
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

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
                            // TODO: error here
                        }
                        return new AstDirectiveStmt(expr, type, new Location(tkn.Location, expr.Location.Ending));
                    }
            }

            // TODO: error here
            return new AstEmptyStmt();
        }
    }
}
