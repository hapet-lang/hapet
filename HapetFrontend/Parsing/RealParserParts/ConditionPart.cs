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

            var beg = Consume(inInfo, TokenType.KwIf, ErrMsg("keyword 'if'", "at beginning of 'if' statement"));

            SkipNewlines(inInfo);

            // parse arguments
            Consume(inInfo, TokenType.OpenParen, ErrMsg("'('", "at the begining of 'if' statement"));

            SkipNewlines(inInfo);

            // if there is a condition param
            if (!CheckToken(inInfo, TokenType.CloseParen))
            {
                var expr = ParseExpression(inInfo, ref outInfo);

                if (expr is not AstExpression)
                    ReportMessage(expr, [], ErrorCode.Get(CTEN.IfStmtCondNotExpr));
                condition = expr as AstExpression;
            }
            else
                ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.IfStmtCondExpected));

            SkipNewlines(inInfo);
            var end = Consume(inInfo, TokenType.CloseParen, ErrMsg("')'", "after the condition"));

            var savedAddBefore = outInfo.StatementsToAddBefore;
            outInfo.StatementsToAddBefore = new List<Ast.Declarations.AstVarDecl>();

            bodyTrue = GetLoopOrCondBlock(inInfo, ref outInfo);

            SkipNewlines(inInfo);

            Token elseTkn = null;
            // if there is an 'else' block
            if (CheckToken(inInfo, TokenType.KwElse))
            {
                elseTkn = Consume(inInfo, TokenType.KwElse, ErrMsg("keyword 'else'", "at beginning of 'else' statement"));
                SkipNewlines(inInfo);

                bodyFalse = GetLoopOrCondBlock(inInfo, ref outInfo);
            }

            outInfo.StatementsToAddBefore = savedAddBefore;

            return new AstIfStmt(condition, bodyTrue, bodyFalse, new Location(beg.Location, end.Location))
            {
                ElseTokenLocation = elseTkn?.Location,
            };
        }

        private AstStatement ParseSwitchStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            AstExpression condition = null;

            var beg = Consume(inInfo, TokenType.KwSwitch, ErrMsg("keyword 'switch'", "at beginning of 'switch' statement"));

            SkipNewlines(inInfo);

            // parse arguments
            Consume(inInfo, TokenType.OpenParen, ErrMsg("'('", "at the begining of 'switch' statement"));

            SkipNewlines(inInfo);

            // if there is a condition param
            if (!CheckToken(inInfo, TokenType.CloseParen))
            {
                var expr = ParseExpression(inInfo, ref outInfo);

                if (expr is not AstExpression)
                    ReportMessage(expr, [], ErrorCode.Get(CTEN.SwitchStmtCondNotExpr));
                condition = expr as AstExpression;
            }
            else
                ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.SwitchStmtCondExpected));

            SkipNewlines(inInfo);
            var end = Consume(inInfo, TokenType.CloseParen, ErrMsg("')'", "after the condition"));

            SkipNewlines(inInfo);

            // parsing the block
            if (CheckToken(inInfo, TokenType.OpenBrace))
            {
                var savedSkip = inInfo.SkipDefaultSemicolonChecks; // skip checks at the top level for 'default'
                var savedDefault = inInfo.ExpectDefaultCase; // skip checks at the top level for 'default'
                inInfo.SkipDefaultSemicolonChecks = true; // skip checks at the top level for 'default'
                inInfo.ExpectDefaultCase = true; 
                var theBlock = ParseBlockExpression(inInfo, ref outInfo);
                inInfo.SkipDefaultSemicolonChecks = savedSkip;
                inInfo.ExpectDefaultCase = savedDefault;

                List<AstCaseStmt> cases = theBlock.Statements.Select(x => x as AstCaseStmt).ToList();
                return new AstSwitchStmt(condition, cases, new Location(beg.Location, end.Location));
            }
            else if (CheckToken(inInfo, TokenType.Semicolon))
            {
                // check if there is not only a '{' but could be a ';'
                // because exprs like 'switch (asd) ;' should also be handled
                // if there is no '{' just create an empty block
                NextToken(inInfo);
                return new AstSwitchStmt(condition, new List<AstCaseStmt>(), new Location(beg.Location, end.Location));
            }
            else
            {
                // error here. it has to have braces
                ReportMessage(new Location(beg.Location, end.Location), [], ErrorCode.Get(CTEN.CasesExpectedAfterSwitch));
                return ParseEmptyExpression(inInfo);
            }
        }

        private AstStatement ParseCaseStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo, bool fromDefault, TokenLocation defaultBegin)
        {
            AstExpression pattern = null;
            AstBlockExpr body = null;
            string gotoLabel = null;
            bool isDefault = fromDefault;
            bool isFalling = false;

            // do not expect default case for now
            var savedDefault = inInfo.ExpectDefaultCase;
            inInfo.ExpectDefaultCase = false;

            TokenLocation beg;
            TokenLocation end;

            // the case could start with 'default' word
            if (fromDefault)
                beg = defaultBegin;
            else
                beg = Consume(inInfo, TokenType.KwCase, ErrMsg("keyword 'case'", "at beginning of 'case' statement")).Location;
            SkipNewlines(inInfo);

            // by default :)
            end = beg;

            // getting an expr after the 'case' word
            if (!isDefault)
            {
                // parse arguments
                Consume(inInfo, TokenType.OpenParen, ErrMsg("'('", "at the begining of 'case' statement"));

                SkipNewlines(inInfo);

                // if there is a condition param
                if (!CheckToken(inInfo, TokenType.CloseParen))
                {
                    var expr = ParseExpression(inInfo, ref outInfo);

                    if (expr is not AstExpression)
                        ReportMessage(expr, [], ErrorCode.Get(CTEN.CaseParamExprExpected));
                    pattern = expr as AstExpression;
                }
                else
                    ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.CaseParamExpected));

                SkipNewlines(inInfo);
                end = Consume(inInfo, TokenType.CloseParen, ErrMsg("')'", "after the pattern")).Location;
            }

            SkipNewlines(inInfo);

            Token gotoTkn = null;
            // check for goto label
            if (CheckToken(inInfo, TokenType.DollarIdentifier))
            {
                gotoTkn = NextToken(inInfo);
                gotoLabel = gotoTkn.Data as string;
            }

            SkipNewlines(inInfo);

            if (CheckToken(inInfo, TokenType.KwCase))
            {
                isFalling = true;
            }
            else
            {
                // parsing the block
                body = GetLoopOrCondBlock(inInfo, ref outInfo);
            }

            inInfo.ExpectDefaultCase = savedDefault;

            var cs = new AstCaseStmt(pattern, body, new Location(beg, end));
            cs.LabelForGoto = gotoLabel;
            cs.IsDefaultCase = isDefault;
            cs.IsFallingCase = isFalling;
            cs.GotoLabelLocation = gotoTkn?.Location;
            return cs;
        }

        private AstStatement ParseSwitchExpression(AstExpression subExpr, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var beg = subExpr.Location.Beginning;
            var end = Consume(inInfo, TokenType.KwSwitch, ErrMsg("keyword 'switch'", "at beginning of 'switch' expression"));

            SkipNewlines(inInfo);

            // parsing the block
            if (CheckToken(inInfo, TokenType.OpenBrace))
            {
                List<AstCaseExpr> cases = new List<AstCaseExpr>();
                var saved = inInfo.CurrentlyParsingExpressedSwitch;
                inInfo.CurrentlyParsingExpressedSwitch = true;

                Consume(inInfo, TokenType.OpenBrace, ErrMsg("{", "at beginning of block expression"));
                SkipNewlines(inInfo);
                while (true)
                {
                    var next = PeekToken(inInfo);
                    if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                        break;

                    var pattern = ParseExpression(inInfo, ref outInfo);
                    if (pattern is not AstExpression) ReportMessage(pattern, [], ErrorCode.Get(CTEN.PatternAsExprExpected));
                    SkipNewlines(inInfo);
                    Consume(inInfo, TokenType.Arrow, ErrMsg("=>", "in pattern expr"));
                    SkipNewlines(inInfo);
                    var returnExpr = ParseExpression(inInfo, ref outInfo);
                    if (returnExpr is not AstExpression) ReportMessage(returnExpr, [], ErrorCode.Get(CTEN.PatternResultAsExprExpected));

                    // expect comma at the end of a pattern
                    // if not a comma and not a closebr - error
                    SkipNewlines(inInfo);
                    if (!CheckToken(inInfo, TokenType.Comma))
                    {
                        SkipNewlines(inInfo);
                        if (!CheckToken(inInfo, TokenType.CloseBrace))
                        {
                            ReportMessage(PeekToken(inInfo).Location, [",", "at the end of pattern expr"], ErrorCode.Get(CTEN.CommonExpectedToken));
                        }
                    }
                    else
                    {
                        // eat ','
                        NextToken(inInfo);
                    }
                    SkipNewlines(inInfo);

                    // add to list of cases
                    cases.Add(new AstCaseExpr(pattern as AstExpression, returnExpr as AstExpression, new Location(pattern.Beginning, returnExpr.Ending)));
                }
                Consume(inInfo, TokenType.CloseBrace, ErrMsg("}", "at end of block expression"));

                inInfo.CurrentlyParsingExpressedSwitch = saved;

                return new AstSwitchExpr(subExpr, cases, new Location(beg, end.Location));
            }
            else
            {
                // error here. it has to have braces
                ReportMessage(new Location(beg, end.Location), [], ErrorCode.Get(CTEN.CasesExpectedAfterSwitch));
                return ParseEmptyExpression(inInfo);
            }
        }
    }
}
