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

            SkipNewlines();
            var token = PeekToken();
            switch (token.Type)
            {
                case TokenType.EOF:
                    toReturn = null;
                    break;

                case TokenType.KwReturn:
                    toReturn = ParseReturnStatement(inInfo, ref outInfo);
                    break;

                case TokenType.KwWhile:
                    toReturn = ParseWhileStatement(inInfo, ref outInfo);
                    break;
                case TokenType.KwFor:
                    toReturn = ParseForStatement(inInfo, ref outInfo);
                    break;
                case TokenType.KwIf:
                    toReturn = ParseIfStatement(inInfo, ref outInfo);
                    break;

                case TokenType.KwSwitch:
                    toReturn = ParseSwitchStatement(inInfo, ref outInfo);
                    break;
                case TokenType.KwCase:
                    toReturn = ParseCaseStatement(inInfo, ref outInfo);
                    break;

                case TokenType.KwContinue:
                case TokenType.KwBreak:
                    NextToken();
                    toReturn = new AstBreakContStmt(token.Type == TokenType.KwBreak, new Location(token.Location));
                    break;

                case TokenType.OpenBracket:
                    toReturn = ParseAttributeStatement(inInfo, ref outInfo);
                    break;

                case TokenType.OpenBrace:
                    toReturn = ParseBlockExpression(inInfo, ref outInfo);
                    break;

                // directive
                case TokenType.SharpIdentifier:
                    toReturn = ParseDirectiveStatement(inInfo, ref outInfo);
                    break;

                default:
                    {
                        var saved4 = inInfo.AllowTypedTuple;
                        inInfo.AllowTypedTuple = true; // ALLOW TYPED TUPLES WHEN DECLS!!!
                        // just handlers
                        AstStatement stmt;
                        if (parseAtomic)
                            stmt = ParseAtomicExpression(inInfo, ref outInfo);
                        else
                            stmt = ParseExpression(inInfo, ref outInfo);
                        SkipNewlines();
                        inInfo.AllowTypedTuple = saved4;

                        // change to udecl like it was previously
                        // https://github.com/hapet-lang/hapet/blob/c15ae05721d3f91fe86a25658ef099d4e84f117f/HapetFrontend/Parsing/RealParserParts/SpecialKeysPart.cs#L62-L69
                        if ((stmt is AstIdExpr) ||
                            (stmt is AstNestedExpr nestExpr && (nestExpr.RightPart is AstIdExpr || nestExpr.RightPart is AstArrayAccessExpr)) ||
                            (stmt is AstPointerExpr ptrExpr && ptrExpr.IsDereference))
                        {
                            stmt = new AstUnknownDecl(new AstNestedExpr(stmt as AstExpression, null, stmt), null, stmt);
                        }
                        else if (stmt is AstTupleExpr tpl)
                        {
                            stmt = PrepareTupleExpr(tpl, inInfo, ref outInfo);
                        }

                        // further preparations
                        if (stmt is AstEmptyStmt)
                        {
                            NextToken();
                            toReturn = stmt;
                        }
                        else if (stmt is AstUnknownDecl udecl)
                        {
                            toReturn = PrepareUnknownDecl(udecl, new List<AstAttributeStmt>(), inInfo, ref outInfo);
                        }
                        else
                        {
                            toReturn = stmt;
                        }
                        break;
                    }
            }

            // skip unneeded
            SkipNewlines();

            return toReturn;
        }
    }
}
