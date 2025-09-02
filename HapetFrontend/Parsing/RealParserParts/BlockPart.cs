using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using System.Collections.Generic;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstBlockExpr ParseBlockExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var statements = new List<AstStatement>();
            var beg = Consume(TokenType.OpenBrace, ErrMsg("{", "at beginning of block expression")).Location;

            // get info and reset this cringe - only top level skips
            var skipDefaultSemicolonChecks = inInfo.SkipDefaultSemicolonChecks;
            inInfo.SkipDefaultSemicolonChecks = false;

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

                statements.AddRange(ParseOneBlockStmt(inInfo, ref outInfo, out bool shouldStop, ref foundBrStatement, ref afterBrStatementReported, skipDefaultSemicolonChecks));
                if (shouldStop)
                    break;

                SkipNewlines();
            }

            var end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of block expression")).Location;

            return new AstBlockExpr(statements, new Location(beg, end));
        }

        /// <summary>
        /// This func tries to parse only one statement. But there could be more than one :)
        /// Because of directives
        /// </summary>
        /// <param name="inInfo"></param>
        /// <param name="outInfo"></param>
        /// <param name="shouldStop"></param>
        /// <returns></returns>
        private List<AstStatement> ParseOneBlockStmt(ParserInInfo inInfo, ref ParserOutInfo outInfo, out bool shouldStop, 
            ref string foundBrStatement, ref bool afterBrStatementReported, bool skipDefaultSemicolonChecks)
        {
            shouldStop = false;

            // get current special keys - useful for nested funcs :)
            var specialKeys = ParseSpecialKeys();
            var statements = new List<AstStatement>();

            // do not allow nested funcs in nested shite. only in current!!!
            var saved1 = inInfo.AllowNestedFunc;
            var saved2 = inInfo.ParentFuncDecl;
            inInfo.AllowNestedFunc = false;
            inInfo.ParentFuncDecl = null;
            var s = ParseStatement(inInfo, ref outInfo);
            inInfo.AllowNestedFunc = saved1;
            inInfo.ParentFuncDecl = saved2;

            if (HandleStatement(s, inInfo, ref outInfo, ref foundBrStatement, ref afterBrStatementReported, true))
                shouldStop = true;

            return statements;

            bool HandleStatement(AstStatement s, ParserInInfo inInfo, ref ParserOutInfo outInfo, 
                ref string foundBrStatement, ref bool afterBrStatementReported, bool checkSemicolons)
            {
                if (s != null)
                {
                    if (s is AstDirectiveStmt dir)
                    {
                        // cringe kostyl to handle directives
                        var saved = inInfo.HandleDirectiveInBlock;
                        inInfo.HandleDirectiveInBlock = true;
                        var statementsToAdd = HandleDirective(dir, CurrentSourceFile, inInfo, ref outInfo);
                        inInfo.HandleDirectiveInBlock = saved;
                        foreach (var stt in statementsToAdd)
                        {
                            // do not check semicolons here - they are already checked in HandleDirective
                            if (HandleStatement(stt, inInfo, ref outInfo, ref foundBrStatement, ref afterBrStatementReported, false))
                                return true;
                        }
                        return false;
                    }

                    // check that stmt is not after return/break OR is a nested function
                    if (string.IsNullOrWhiteSpace(foundBrStatement) || s is AstFuncDecl)
                    {
                        if (outInfo.StatementsToAddBefore.Count > 0)
                        {
                            statements.AddRange(outInfo.StatementsToAddBefore);
                            outInfo.StatementsToAddBefore.Clear();
                        }

                        statements.Add(s);

                        if (outInfo.StatementsToAddAfter.Count > 0)
                        {
                            statements.AddRange(outInfo.StatementsToAddAfter);
                            outInfo.StatementsToAddAfter.Clear();
                        }
                    }
                    else if (!afterBrStatementReported)
                    {
                        // print warning that the line won't be accepted
                        // print the warning only once, do not spam
                        afterBrStatementReported = true;
                        ReportMessage(s, [foundBrStatement], ErrorCode.Get(CTWN.StmtsWouldBeIgnored), Entities.ReportType.Warning);
                    }

                    if (checkSemicolons)
                        // try eat semicolon or error
                        CheckSemicolonAfterStmt(s, skipDefaultSemicolonChecks);

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

                    // check for nested func
                    if (s is AstFuncDecl nestedFunc)
                    {
                        if (!inInfo.AllowNestedFunc)
                        {
                            // error here that is not expected
                            ReportMessage(nestedFunc.Name, [], ErrorCode.Get(CTEN.UnexpectedNestedFunc));
                        }

                        nestedFunc.IsNestedDecl = true;
                        nestedFunc.ParentDecl = inInfo.ParentFuncDecl;
                        if (specialKeys != null)
                        {
                            nestedFunc.SpecialKeys.AddRange(specialKeys);
                            specialKeys = null;
                        }
                        _compiler.LambdasAndNested.Add(nestedFunc);

                        // for now only static allowed
                        if (!nestedFunc.SpecialKeys.Contains(TokenType.KwStatic))
                        {
                            // error here that is not expected
                            ReportMessage(nestedFunc.Name, [], ErrorCode.Get(CTEN.NonStaticNestedLambda));
                        }
                    }

                    var next = PeekToken();
                    if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                        return true;
                }
                return false;
            }
        }
    }
}
