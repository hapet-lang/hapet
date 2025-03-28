using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Errors;
using HapetFrontend.Entities;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstIfStmt ParseIfStatement()
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            AstExpression condition = null;
            AstBlockExpr bodyTrue;
            AstBlockExpr bodyFalse = null;

            var beg = Consume(TokenType.KwIf, ErrMsg("keyword 'if'", "at beginning of 'if' statement"));

            // parse arguments
            Consume(TokenType.OpenParen, ErrMsg("'('", "at the begining of 'if' statement"));

            // if there is a condition param
            if (!CheckToken(TokenType.CloseParen))
            {
                var expr = ParseExpression(inInfo, ref outInfo);

                if (expr is not AstExpression)
                    ReportMessage(expr, [], ErrorCode.Get(CTEN.IfStmtCondNotExpr));
                condition = expr as AstExpression;
            }
            else
                ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.IfStmtCondExpected));
            var end = Consume(TokenType.CloseParen, ErrMsg("')'", "after the condition"));

            SkipNewlines();

            // parsing the block
            if (CheckToken(TokenType.OpenBrace))
            {
                bodyTrue = ParseBlockExpression(inInfo, ref outInfo);
            }
            else if (CheckToken(TokenType.Semicolon))
            {
                // check if there is not only a '{' but could be a ';'
                // because exprs like 'if (false) ;' should also be handled
                // if there is no '{' just create an empty block
                NextToken();
                bodyTrue = new AstBlockExpr(new List<AstStatement>(), PeekToken().Location);
            }
            else
            {
                // getting only one stmt if there are no braces
                var onlyStmt = ParseStatement(inInfo, ref outInfo);
                bodyTrue = new AstBlockExpr(new List<AstStatement>() { onlyStmt }, onlyStmt);
            }

            SkipNewlines();

            // if there is an 'else' block
            if (CheckToken(TokenType.KwElse))
            {
                Consume(TokenType.KwElse, ErrMsg("keyword 'else'", "at beginning of 'else' statement"));
                SkipNewlines();

                // parsing the block
                if (CheckToken(TokenType.OpenBrace))
                {
                    bodyFalse = ParseBlockExpression(inInfo, ref outInfo);
                }
                else
                {
                    // getting only one stmt if there are no braces
                    var onlyStmt = ParseStatement(inInfo, ref outInfo);
                    bodyFalse = new AstBlockExpr(new List<AstStatement>() { onlyStmt }, onlyStmt);
                }
            }

            return new AstIfStmt(condition, bodyTrue, bodyFalse, new Location(beg.Location, end.Location));
        }

        private AstStatement ParseSwitchStatement()
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            AstExpression condition = null;

            var beg = Consume(TokenType.KwSwitch, ErrMsg("keyword 'switch'", "at beginning of 'switch' statement"));

            // parse arguments
            Consume(TokenType.OpenParen, ErrMsg("'('", "at the begining of 'switch' statement"));

            // if there is a condition param
            if (!CheckToken(TokenType.CloseParen))
            {
                var expr = ParseExpression(inInfo, ref outInfo);

                if (expr is not AstExpression)
                    ReportMessage(expr, [], ErrorCode.Get(CTEN.SwitchStmtCondNotExpr));
                condition = expr as AstExpression;
            }
            else
                ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.SwitchStmtCondExpected));
            var end = Consume(TokenType.CloseParen, ErrMsg("')'", "after the condition"));

            SkipNewlines();

            // parsing the block
            if (CheckToken(TokenType.OpenBrace))
            {
                var theBlock = ParseBlockExpression(inInfo, ref outInfo);
                List<AstCaseStmt> cases = new List<AstCaseStmt>();

                // serching for default
                AstExpression prevWasDefault = null;
                foreach (var s in theBlock.Statements)
                {
                    if (s is not AstCaseStmt caseStmt)
                    {
                        if (s is AstDefaultExpr defExpr)
                        {
                            prevWasDefault = defExpr;
                            continue;
                        }

                        // this cringe is done to handle default case :((
                        if (s is AstBlockExpr block && prevWasDefault != null)
                        {
                            cases.Add(new AstCaseStmt(null, block, prevWasDefault) { IsDefaultCase = true });
                            prevWasDefault = null;
                        }
                        else if (s is AstStatement stmt && prevWasDefault != null)
                        {
                            cases.Add(new AstCaseStmt(null, new AstBlockExpr(new List<AstStatement>() { stmt }, stmt), prevWasDefault) { IsDefaultCase = true });
                            prevWasDefault = null;
                        }
                        else
                        {
                            // error here. all the statements have to be cases
                            ReportMessage(s, [], ErrorCode.Get(CTEN.CaseExpectedToBeStmt));
                        }
                        continue;
                    }
                    cases.Add(caseStmt);
                }

                return new AstSwitchStmt(condition, cases, new Location(beg.Location, end.Location));
            }
            else if (CheckToken(TokenType.Semicolon))
            {
                // check if there is not only a '{' but could be a ';'
                // because exprs like 'switch (asd) ;' should also be handled
                // if there is no '{' just create an empty block
                NextToken();
                return new AstSwitchStmt(condition, new List<AstCaseStmt>(), new Location(beg.Location, end.Location));
            }
            else
            {
                // error here. it has to have braces
                ReportMessage(new Location(beg.Location, end.Location), [], ErrorCode.Get(CTEN.CasesExpectedAfterSwitch));
                return ParseEmptyExpression();
            }
        }

        private AstStatement ParseCaseStatement()
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            AstExpression pattern = null;
            AstBlockExpr body = null;
            bool isDefault = false;
            bool isFalling = false;

            TokenLocation beg;
            TokenLocation end;

            // the case could start with 'default' word
            if (CheckToken(TokenType.KwDefault))
            {
                isDefault = true;
                beg = Consume(TokenType.KwDefault, ErrMsg("keyword 'default'", "at beginning of 'default' case statement")).Location;
            }
            else
            {
                beg = Consume(TokenType.KwCase, ErrMsg("keyword 'case'", "at beginning of 'case' statement")).Location;
            }

            // by default :)
            end = beg;

            // getting an expr after the 'case' word
            if (!isDefault)
            {
                // parse arguments
                Consume(TokenType.OpenParen, ErrMsg("'('", "at the begining of 'case' statement"));

                // if there is a condition param
                if (!CheckToken(TokenType.CloseParen))
                {
                    var expr = ParseExpression(inInfo, ref outInfo);

                    if (expr is not AstExpression)
                        ReportMessage(expr, [], ErrorCode.Get(CTEN.CaseParamExprExpected));
                    pattern = expr as AstExpression;
                }
                else
                    ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.CaseParamExpected));
                end = Consume(TokenType.CloseParen, ErrMsg("')'", "after the pattern")).Location;
            }

            SkipNewlines();

            // parsing the block
            if (CheckToken(TokenType.OpenBrace))
            {
                body = ParseBlockExpression(inInfo, ref outInfo);
            }
            else if (CheckToken(TokenType.KwCase))
            {
                isFalling = true;
            }
            else
            {
                // getting only one stmt if there are no braces
                var onlyStmt = ParseStatement(inInfo, ref outInfo);
                body = new AstBlockExpr(new List<AstStatement>() { onlyStmt }, onlyStmt);
            }

            var cs = new AstCaseStmt(pattern, body, new Location(beg, end));
            cs.IsDefaultCase = isDefault;
            cs.IsFallingCase = isFalling;
            return cs;
        }
    }
}
