using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstBlockExpr ParseBlockExpression()
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            var statements = new List<AstStatement>();
            var beg = Consume(TokenType.OpenBrace, ErrMsg("{", "at beginning of block expression")).Location;

            // the string is used to check if BR found in the block
            // so do not accept any statements after it
            string foundBrStatement = string.Empty;
            bool afterBrStatementReported = false;

            SkipNewlines();
            while (true)
            {
                var next = PeekToken();
                if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                    break;

                inInfo.ExpectNewline = false;
                var s = ParseStatement(inInfo, ref outInfo);
                inInfo.ExpectNewline = true;

                if (s != null)
                {
                    if (string.IsNullOrWhiteSpace(foundBrStatement))
                    {
                        statements.Add(s);
                    }
                    else if (!afterBrStatementReported)
                    {
                        // print warning that the line won't be accepted
                        // print the warning only once, do not spam
                        afterBrStatementReported = true;
                        ReportMessage(s, [foundBrStatement], ErrorCode.Get(CTWN.StmtsWouldBeIgnored), Entities.ReportType.Warning);
                    }

                    next = PeekToken();

                    if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                        break;

                    // save the statment name to warn if there is something after it
                    switch (s)
                    {
                        case AstReturnStmt:
                            foundBrStatement = "return";
                            break;
                        case AstBreakContStmt bc:
                            foundBrStatement = bc.IsBreak ? "break" : "continue";
                            break;
                    }

                }
                SkipNewlines();
            }

            var end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of block expression")).Location;

            return new AstBlockExpr(statements, new Location(beg, end));
        }
    }
}
