using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using System.Collections.Generic;
using HapetFrontend.Errors;
using HapetFrontend.Entities;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstForStmt ParseForStatement()
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            AstStatement first = null;
            AstExpression second = null;
            AstStatement third = null;
            AstBlockExpr body;

            var beg = Consume(TokenType.KwFor, ErrMsg("keyword 'for'", "at beginning of 'for' loop"));

            // parse arguments
            Consume(TokenType.OpenParen, ErrMsg("'('", "at the begining of 'for' loop statement"));

            // if there is a first param
            if (!CheckToken(TokenType.Semicolon))
            {
                inInfo.ExpectNewline = false;
                first = ParseStatement(inInfo, ref outInfo);
                inInfo.ExpectNewline = true;
            }
            // Consume(TokenType.Semicolon, ErrMsg("';'", "after the first argument")); // WARN: semicolon is parsed inside ParseStatement

            // if there is a second param
            if (!CheckToken(TokenType.Semicolon))
            {
                inInfo.AllowCommaForTuple = true;
                var expr = ParseExpression(inInfo, ref outInfo);
                inInfo.AllowCommaForTuple = false;

                if (expr is not AstExpression)
                    ReportMessage(expr, [], ErrorCode.Get(CTEN.ForLoopSecondNotExpr));
                second = expr as AstExpression;
            }
            Consume(TokenType.Semicolon, ErrMsg("';'", "after the second argument"));

            // if there is a third param
            if (!CheckToken(TokenType.CloseParen))
            {
                inInfo.ExpectNewline = false;
                third = ParseStatement(inInfo, ref outInfo);
                inInfo.ExpectNewline = true;
            }
            var end = Consume(TokenType.CloseParen, ErrMsg("')'", "after the third argument"));

            SkipNewlines();

            // parsing the block
            if (CheckToken(TokenType.OpenBrace))
            {
                body = ParseBlockExpression();
            }
            else if (CheckToken(TokenType.Semicolon))
            {
                // check if there is not only a '{' but could be a ';'
                // because exprs like 'for (;;) ;' should also be handled
                // if there is no '{' just create an empty block
                NextToken();
                body = new AstBlockExpr(new List<AstStatement>(), PeekToken().Location);
            }
            else
            {
                // getting only one stmt if there are no braces
                inInfo.ExpectNewline = false;
                var onlyStmt = ParseStatement(inInfo, ref outInfo);
                inInfo.ExpectNewline = true;
                body = new AstBlockExpr(new List<AstStatement>() { onlyStmt }, onlyStmt);
            }

            return new AstForStmt(first, second, third, body, new Location(beg.Location, end.Location));
        }

        private AstWhileStmt ParseWhileStatement()
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            AstExpression condition = null;
            AstBlockExpr body;

            var beg = Consume(TokenType.KwWhile, ErrMsg("keyword 'while'", "at beginning of 'while' loop"));

            // parse arguments
            Consume(TokenType.OpenParen, ErrMsg("'('", "at the begining of 'while' loop statement"));

            // if there is a condition param
            if (!CheckToken(TokenType.CloseParen))
            {
                inInfo.AllowCommaForTuple = true;
                var expr = ParseExpression(inInfo, ref outInfo);
                inInfo.AllowCommaForTuple = false;

                if (expr is not AstExpression)
                    ReportMessage(expr, [], ErrorCode.Get(CTEN.WhileLoopParamNotExpr));
                condition = expr as AstExpression;
            }
            else
                ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.WhileLoopNoCondition));
            var end = Consume(TokenType.CloseParen, ErrMsg("')'", "after the condition"));

            SkipNewlines();

            // parsing the block
            if (CheckToken(TokenType.OpenBrace))
            {
                body = ParseBlockExpression();
            }
            else if (CheckToken(TokenType.Semicolon))
            {
                // check if there is not only a '{' but could be a ';'
                // because exprs like 'while (false) ;' should also be handled
                // if there is no '{' just create an empty block
                NextToken();
                body = new AstBlockExpr(new List<AstStatement>(), PeekToken().Location);
            }
            else
            {
                // getting only one stmt if there are no braces
                inInfo.ExpectNewline = false;
                var onlyStmt = ParseStatement(inInfo, ref outInfo);
                inInfo.ExpectNewline = true;
                body = new AstBlockExpr(new List<AstStatement>() { onlyStmt }, onlyStmt);
            }

            return new AstWhileStmt(condition, body, new Location(beg.Location, end.Location));
        }
    }
}
