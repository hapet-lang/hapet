using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Errors;
using HapetFrontend.Entities;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstIfStmt ParseIfStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
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

            bodyTrue = GetLoopOrCondBlock(inInfo, ref outInfo);

            SkipNewlines();

            // if there is an 'else' block
            if (CheckToken(TokenType.KwElse))
            {
                Consume(TokenType.KwElse, ErrMsg("keyword 'else'", "at beginning of 'else' statement"));
                SkipNewlines();

                bodyFalse = GetLoopOrCondBlock(inInfo, ref outInfo);
            }

            return new AstIfStmt(condition, bodyTrue, bodyFalse, new Location(beg.Location, end.Location));
        }

        private AstStatement ParseSwitchStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
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
                var savedSkip = inInfo.SkipDefaultSemicolonChecks; // skip checks at the top level for 'default'
                inInfo.SkipDefaultSemicolonChecks = true; // skip checks at the top level for 'default'
                var theBlock = ParseBlockExpression(inInfo, ref outInfo);
                inInfo.SkipDefaultSemicolonChecks = savedSkip;

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

        private AstStatement ParseCaseStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            AstExpression pattern = null;
            AstBlockExpr body = null;
            string gotoLabel = null;
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

            // check for goto label
            if (CheckToken(TokenType.Identifier))
            {
                var expr = ParseExpression(inInfo, ref outInfo);
                if (!(expr is AstNestedExpr nst && nst.RightPart is AstIdExpr idExpr))
                {
                    // error here
                    ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                    return new AstEmptyStmt();
                }
                gotoLabel = idExpr.Name;
            }

            SkipNewlines();

            if (CheckToken(TokenType.KwCase))
            {
                isFalling = true;
            }
            else
            {
                // parsing the block
                body = GetLoopOrCondBlock(inInfo, ref outInfo);
            }

            var cs = new AstCaseStmt(pattern, body, new Location(beg, end));
            cs.LabelForGoto = gotoLabel;
            cs.IsDefaultCase = isDefault;
            cs.IsFallingCase = isFalling;
            return cs;
        }
    }
}
