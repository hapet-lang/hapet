using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using System.Collections.Generic;
using HapetFrontend.Errors;
using HapetFrontend.Entities;
using System.Runtime;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstForStmt ParseForStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            AstStatement first = null;
            AstExpression second = null;
            AstStatement third = null;
            AstBlockExpr body;

            var beg = Consume(inInfo, TokenType.KwFor, ErrMsg("keyword 'for'", "at beginning of 'for' loop"));

            // parse arguments
            Consume(inInfo, TokenType.OpenParen, ErrMsg("'('", "at the begining of 'for' loop statement"));

            // if there is a first param
            if (!CheckToken(inInfo, TokenType.Semicolon))
            {
                first = ParseStatement(inInfo, ref outInfo);
            }
            Consume(inInfo, TokenType.Semicolon, ErrMsg("';'", "after the first argument"));

            // if there is a second param
            if (!CheckToken(inInfo, TokenType.Semicolon))
            {
                var expr = ParseExpression(inInfo, ref outInfo);

                if (expr is not AstExpression)
                    ReportMessage(expr, [], ErrorCode.Get(CTEN.ForLoopSecondNotExpr));
                second = expr as AstExpression;
            }
            Consume(inInfo, TokenType.Semicolon, ErrMsg("';'", "after the second argument"));

            // if there is a third param
            if (!CheckToken(inInfo, TokenType.CloseParen))
            {
                third = ParseStatement(inInfo, ref outInfo);
            }
            var end = Consume(inInfo, TokenType.CloseParen, ErrMsg("')'", "after the third argument"));

            body = GetLoopOrCondBlock(inInfo, ref outInfo);

            return new AstForStmt(first, second, third, body, new Location(beg.Location, end.Location));
        }

        private AstWhileStmt ParseWhileStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            AstExpression condition = null;
            AstBlockExpr body;

            var beg = Consume(inInfo, TokenType.KwWhile, ErrMsg("keyword 'while'", "at beginning of 'while' loop"));

            // parse arguments
            Consume(inInfo, TokenType.OpenParen, ErrMsg("'('", "at the begining of 'while' loop statement"));

            // if there is a condition param
            if (!CheckToken(inInfo, TokenType.CloseParen))
            {
                var expr = ParseExpression(inInfo, ref outInfo);

                if (expr is not AstExpression)
                    ReportMessage(expr, [], ErrorCode.Get(CTEN.WhileLoopParamNotExpr));
                condition = expr as AstExpression;
            }
            else
                ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.WhileLoopNoCondition));
            var end = Consume(inInfo, TokenType.CloseParen, ErrMsg("')'", "after the condition"));

            body = GetLoopOrCondBlock(inInfo, ref outInfo);

            return new AstWhileStmt(condition, body, new Location(beg.Location, end.Location));
        }

        private AstDoWhileStmt ParseDoWhileStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            AstExpression condition = null;
            AstBlockExpr body;

            var beg = Consume(inInfo, TokenType.KwDo, ErrMsg("keyword 'do'", "at beginning of 'do-while' loop"));

            // parse its body
            body = GetLoopOrCondBlock(inInfo, ref outInfo);

            SkipNewlines(inInfo);
            var whileTkn = Consume(inInfo, TokenType.KwWhile, ErrMsg("keyword 'while'", "at the end of 'do-while' loop"));

            // parse arguments
            Consume(inInfo, TokenType.OpenParen, ErrMsg("'('", "at the begining of 'while' keyword"));

            // if there is a condition param
            if (!CheckToken(inInfo, TokenType.CloseParen))
            {
                var expr = ParseExpression(inInfo, ref outInfo);

                if (expr is not AstExpression)
                    ReportMessage(expr, [], ErrorCode.Get(CTEN.WhileLoopParamNotExpr));
                condition = expr as AstExpression;
            }
            else
                ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.WhileLoopNoCondition));
            var end = Consume(inInfo, TokenType.CloseParen, ErrMsg("')'", "after the condition"));

            return new AstDoWhileStmt(condition, body, new Location(beg.Location, end.Location))
            {
                WhileTokenLocation = whileTkn.Location,
            };
        }

        private AstBlockExpr GetLoopOrCondBlock(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            SkipNewlines(inInfo);

            AstBlockExpr body;
            // parsing the block
            if (CheckToken(inInfo, TokenType.OpenBrace))
            {
                body = ParseBlockExpression(inInfo, ref outInfo);
            }
            else if (CheckToken(inInfo, TokenType.Semicolon))
            {
                // check if there is not only a '{' but could be a ';'
                // because exprs like 'while (false) ;' should also be handled
                // if there is no '{' just create an empty block
                NextToken(inInfo);
                body = new AstBlockExpr(new List<AstStatement>(), PeekToken(inInfo).Location);
            }
            else
            {
                // getting only one stmt if there are no braces
                var onlyStmt = ParseStatement(inInfo, ref outInfo);
                body = new AstBlockExpr(new List<AstStatement>() { onlyStmt }, onlyStmt);

                // try eat semicolon or error
                CheckSemicolonAfterStmt(inInfo, onlyStmt);

                if (outInfo.StatementsToAddBefore.Count > 0)
                {
                    body.Statements.InsertRange(0, outInfo.StatementsToAddBefore);
                    outInfo.StatementsToAddBefore.Clear();
                }
                if (outInfo.StatementsToAddAfter.Count > 0)
                {
                    body.Statements.AddRange(outInfo.StatementsToAddAfter);
                    outInfo.StatementsToAddAfter.Clear();
                }
            }
            return body;
        }
    }
}
