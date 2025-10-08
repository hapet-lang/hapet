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

            // parse arguments
            Consume(inInfo, TokenType.OpenParen, ErrMsg("'('", "at the begining of 'if' statement"));

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

            // parse arguments
            Consume(inInfo, TokenType.OpenParen, ErrMsg("'('", "at the begining of 'switch' statement"));

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

            // by default :)
            end = beg;

            // getting an expr after the 'case' word
            if (!isDefault)
            {
                // parse arguments
                Consume(inInfo, TokenType.OpenParen, ErrMsg("'('", "at the begining of 'case' statement"));

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
                end = Consume(inInfo, TokenType.CloseParen, ErrMsg("')'", "after the pattern")).Location;
            }

            SkipNewlines(inInfo);

            // check for goto label
            if (CheckToken(inInfo, TokenType.DollarIdentifier))
            {
                var lbl = NextToken(inInfo);
                gotoLabel = lbl.Data as string;
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
            return cs;
        }
    }
}
