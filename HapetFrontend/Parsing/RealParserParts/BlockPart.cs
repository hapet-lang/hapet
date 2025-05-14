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

                // get current special keys - useful for nested funcs :)
                List<Token> specialKeys = ParseSpecialKeys(); 

                // do not allow nested funcs in nested shite. only in current!!!
                var saved1 = inInfo.AllowNestedFunc;
                var saved2 = inInfo.ParentFuncDecl;
                inInfo.AllowNestedFunc = false;
                inInfo.ParentFuncDecl = null;
                var s = ParseStatement(inInfo, ref outInfo);
                inInfo.AllowNestedFunc = saved1;
                inInfo.ParentFuncDecl = saved2;

                if (s != null)
                {
                    // check that stmt is not after return/break OR is a nested function
                    if (string.IsNullOrWhiteSpace(foundBrStatement) || s is AstFuncDecl)
                    {
                        // at first we need to add all cringe 'is' decls
                        // store the decls
                        // list of all additions of declarations
                        // like 'test is Anime anime' so we add 'Anime anime = test as Anime;'
                        // decl before the stmt
                        if (outInfo.IsOpDeclarations.Count > 0)
                        {
                            statements.AddRange(outInfo.IsOpDeclarations.ToList()); // clone them (for what?)
                            outInfo.IsOpDeclarations.Clear();
                        }

                        statements.Add(s);
                    }
                    else if (!afterBrStatementReported)
                    {
                        // print warning that the line won't be accepted
                        // print the warning only once, do not spam
                        afterBrStatementReported = true;
                        ReportMessage(s, [foundBrStatement], ErrorCode.Get(CTWN.StmtsWouldBeIgnored), Entities.ReportType.Warning);
                    }

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
                        nestedFunc.SpecialKeys.AddRange(specialKeys);
                    }

                    next = PeekToken();
                    if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                        break;
                }
                SkipNewlines();
            }

            var end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of block expression")).Location;

            return new AstBlockExpr(statements, new Location(beg, end));
        }
    }
}
