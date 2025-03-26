using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        internal AstStatement ParseStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo, bool parseAtomic = false)
        {
            AstStatement toReturn = null;
            bool semicolonRequired = false;

            SkipNewlines();
            var token = PeekToken();
            switch (token.Type)
            {
                case TokenType.EOF:
                    toReturn = null;
                    break;

                case TokenType.KwReturn:
                    toReturn = ParseReturnStatement();
                    semicolonRequired = true;
                    break;

                case TokenType.KwWhile:
                    toReturn = ParseWhileStatement();
                    break;
                case TokenType.KwFor:
                    toReturn = ParseForStatement();
                    break;
                case TokenType.KwIf:
                    toReturn = ParseIfStatement();
                    break;

                case TokenType.KwSwitch:
                    toReturn = ParseSwitchStatement();
                    break;
                case TokenType.KwCase:
                    toReturn = ParseCaseStatement();
                    break;

                case TokenType.KwContinue:
                case TokenType.KwBreak:
                    NextToken();
                    toReturn = new AstBreakContStmt(token.Type == TokenType.KwBreak, new Location(token.Location));
                    semicolonRequired = true;
                    break;

                case TokenType.OpenBracket:
                    toReturn = ParseAttributeStatement();
                    break;

                case TokenType.OpenBrace:
                    toReturn = ParseBlockExpression();
                    break;

                // directive
                case TokenType.SharpIdentifier:
                    toReturn = ParseDirectiveStatement();
                    break;

                default:
                    {
                        // just handlers
                        AstStatement stmt;
                        if (parseAtomic)
                            stmt = ParseAtomicExpression(inInfo, ref outInfo);
                        else
                            stmt = ParseExpression(inInfo, ref outInfo);
                        SkipNewlines();

                        if (stmt is AstEmptyStmt)
                        {
                            NextToken();
                            toReturn = stmt;
                        }
                        else if (stmt is AstUnknownDecl udecl)
                        {
                            var dcl = PrepareUnknownDecl(udecl, new List<AstAttributeStmt>(), inInfo, ref outInfo, ref semicolonRequired);
                            toReturn = dcl;
                        }
                        else if (stmt is AstDeclaration)
                        {
                            // no need for semicolon
                            toReturn = stmt;
                        }
                        else
                        {
                            toReturn = stmt;
                            semicolonRequired = true;
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
                            var val = ParseExpression(inInfo3, ref outInfo3);

                            if (val is not AstExpression valExpr)
                            {
                                ReportMessage(val.Location, [], ErrorCode.Get(CTEN.RightSideVarDeclNotExpr));
                                toReturn = stmt;
                                break;
                            }

                            if (stmt is AstNestedExpr id && currT.Type != TokenType.Equal)
                            {
                                // expand ops like 'a += b' into 'a = a + b'
                                var binOpExpr = new AstBinaryExpr(op, id, valExpr, new Location(id.Location.Beginning, val.Location.Ending));
                                toReturn = new AstAssignStmt(id, binOpExpr, new Location(stmt.Beginning, val.Ending));
                                semicolonRequired = true;
                            }
                            else if (stmt is AstNestedExpr nestId && currT.Type == TokenType.Equal)
                            {
                                toReturn = new AstAssignStmt(nestId, valExpr, new Location(stmt.Beginning, val.Ending));
                                semicolonRequired = true;
                            }
                        }
                        break;
                    }
            }

            // consume semicolon after some top level statements
            var a = PeekToken();
            if (semicolonRequired)
                Consume(TokenType.Semicolon, ErrMsg(";", "at the end of the statement"));

            // skip unneeded
            SkipNewlines();

            return toReturn;
        }
    }
}
