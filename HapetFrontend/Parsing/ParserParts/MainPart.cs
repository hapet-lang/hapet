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
        internal AstStatement ParseStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var stmt = ParseStatementHelper(inInfo, ref outInfo);

            if (stmt == null)
                return null;

            if (CheckToken(TokenType.Semicolon))
            {
                // TODO: haha is this cringe?
                Consume(TokenType.Semicolon, ErrMsg(";", "at the end of the statement"));
            }

            var next = PeekToken();
            if (inInfo.ExpectNewline && next.Type != TokenType.NewLine && next.Type != TokenType.EOF)
            {
                ReportMessage(next.Location, [], ErrorCode.Get(CTEN.NewlineExpected));
                RecoverStatement();
            }
            return stmt;
        }

        private AstStatement ParseStatementHelper(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            SkipNewlines();
            var token = PeekToken();
            switch (token.Type)
            {
                case TokenType.EOF:
                    return null;

                case TokenType.KwReturn:
                    return ParseReturnStatement();

                case TokenType.KwWhile:
                    return ParseWhileStatement();
                case TokenType.KwFor:
                    return ParseForStatement();
                case TokenType.KwIf:
                    return ParseIfStatement();

                case TokenType.KwSwitch:
                    return ParseSwitchStatement();
                case TokenType.KwCase:
                    return ParseCaseStatement();

                case TokenType.KwContinue:
                case TokenType.KwBreak:
                    NextToken();
                    return new AstBreakContStmt(token.Type == TokenType.KwBreak, new Location(token.Location));

                case TokenType.KwUsing:
                    return ParseUsingStatement();
                case TokenType.KwNamespace:
                    return ParseNamespaceStatement();

                case TokenType.OpenBrace:
                    return ParseBlockExpression();

                default:
                    {
                        // just handlers
                        ParserInInfo inInfo2 = ParserInInfo.Default;
                        ParserOutInfo outInfo2 = ParserOutInfo.Default;
                        inInfo2.AllowCommaForTuple = true;
                        inInfo2.AllowPointerExpression = true;
                        inInfo2.AllowGeneric = true;
                        var stmt = ParseExpression(inInfo2, ref outInfo2); // anyway it should return AstStatement, not AstExpression

                        if (stmt is AstEmptyStmt)
                        {
                            NextToken();
                            return stmt;
                        }
                        if (stmt is AstUnknownDecl udecl)
                        {
                            bool savedAllowence = inInfo.AllowCommaForTuple;
                            inInfo.AllowCommaForTuple = true;
                            var dcl = PrepareUnknownDecl(udecl, new List<AstAttributeStmt>(), inInfo, ref outInfo);
                            inInfo.AllowCommaForTuple = savedAllowence;
                            return dcl;
                        }
                        if (CheckTokens(TokenType.Equal, TokenType.AddEq, TokenType.SubEq, TokenType.MulEq, TokenType.DivEq, TokenType.ModEq))
                        {
                            var currT = NextToken();
                            var x = currT.Type;
                            string op = null;
                            switch (x)
                            {
                                case TokenType.AddEq: op = "+"; break;
                                case TokenType.SubEq: op = "-"; break;
                                case TokenType.MulEq: op = "*"; break;
                                case TokenType.DivEq: op = "/"; break;
                                case TokenType.ModEq: op = "%"; break;
                            }
                            SkipNewlines();

                            // just handlers
                            ParserInInfo inInfo3 = ParserInInfo.Default;
                            ParserOutInfo outInfo3 = ParserOutInfo.Default;
                            inInfo2.AllowCommaForTuple = true;
                            var val = ParseExpression(inInfo3, ref outInfo3);

                            if (val is not AstExpression valExpr)
                            {
                                ReportMessage(val.Location, [], ErrorCode.Get(CTEN.RightSideVarDeclNotExpr));
                                return stmt;
                            }

                            if (stmt is AstNestedExpr id && currT.Type != TokenType.Equal)
                            {
                                // expand ops like 'a += b' into 'a = a + b'
                                var binOpExpr = new AstBinaryExpr(op, id, val, new Location(id.Location.Beginning, val.Location.Ending));
                                return new AstAssignStmt(id, binOpExpr, new Location(stmt.Beginning, val.Ending));
                            }
                            else if (stmt is AstNestedExpr nestId && currT.Type == TokenType.Equal)
                            {
                                return new AstAssignStmt(nestId, valExpr, new Location(stmt.Beginning, val.Ending));
                            }
                        }
                        else
                        {
                            return stmt;
                        }
                        return stmt;
                    }
            }
        }
    }
}
