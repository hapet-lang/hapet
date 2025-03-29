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
                    toReturn = ParseReturnStatement();
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
                    break;

                case TokenType.OpenBracket:
                    toReturn = ParseAttributeStatement();
                    break;

                case TokenType.OpenBrace:
                    toReturn = ParseBlockExpression(inInfo, ref outInfo);
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

                        // change to udecl like it was previously
                        // https://github.com/hapet-lang/hapet/blob/c15ae05721d3f91fe86a25658ef099d4e84f117f/HapetFrontend/Parsing/RealParserParts/SpecialKeysPart.cs#L62-L69
                        if (stmt is AstIdExpr idExpr)
                        {
                            stmt = new AstUnknownDecl(idExpr, null, stmt);
                        }
                        else if (stmt is AstNestedExpr nestExpr && (nestExpr.RightPart is AstIdExpr || nestExpr.RightPart is AstArrayAccessExpr))
                        {
                            stmt = new AstUnknownDecl(nestExpr, null, stmt);
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
