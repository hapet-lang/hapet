using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using System.Diagnostics.Metrics;
using System.Runtime;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseDirectiveStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var tkn = Consume(TokenType.SharpIdentifier, ErrMsg("char '#'", "at beginning of directive"));
            DirectiveType type = DirectiveType.None;

            switch ((string)tkn.Data)
            {
                case "file": type = DirectiveType.MetadataFile; break;
                case "meta": type = DirectiveType.MetadataMeta; break;
                case "endmeta": type = DirectiveType.MetadataEndMeta; break;

                case "if": type = DirectiveType.If; break;
                case "elif": type = DirectiveType.Elif; break;
                case "else": type = DirectiveType.Else; break;
                case "endif": type = DirectiveType.EndIf; break;

                case "define": type = DirectiveType.Define; break;
            }

            switch (type) 
            {
                case DirectiveType.MetadataFile:
                    {
                        var expr = ParseExpression(inInfo, ref outInfo);
                        if (expr is not AstStringExpr)
                        {
                            // error here
                            ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.CommonStringExpected));
                        }
                        return new AstDirectiveStmt(expr, type, new Location(tkn.Location, expr.Location.Ending));
                    }
                case DirectiveType.MetadataMeta:
                case DirectiveType.MetadataEndMeta:
                case DirectiveType.Else:
                case DirectiveType.EndIf:
                    {
                        return new AstDirectiveStmt(null, type, new Location(tkn.Location, tkn.Location.Ending));
                    }
                case DirectiveType.If: 
                case DirectiveType.Elif: 
                    {
                        var expr = ParseExpression(inInfo, ref outInfo);
                        return new AstDirectiveStmt(expr, type, new Location(tkn.Location, expr.Location.Ending));
                    }
                case DirectiveType.Define:
                    {
                        var expr = ParseExpression(inInfo, ref outInfo);
                        if (!(expr is AstNestedExpr nst && nst.RightPart is AstIdExpr))
                        {
                            // error here
                            ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                        }
                        return new AstDirectiveStmt(expr, type, new Location(tkn.Location, expr.Location.Ending));
                    }
            }

            // error here
            ReportMessage(tkn.Location, [], ErrorCode.Get(CTEN.UnexpectedDirective));
            return new AstEmptyStmt();
        }

        internal List<AstStatement> HandleIfDirective(AstDirectiveStmt ifDir, ProgramFile file, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            List<AstStatement> toReturn = null;
            var currentDir = ifDir;
            while (currentDir.DirectiveType != DirectiveType.EndIf)
            {
                if (currentDir.DirectiveType == DirectiveType.Else)
                {
                    toReturn = GetUpToNextDirective(inInfo, ref outInfo);
                    break;
                }

                if (currentDir.RightPart is AstExpression expr)
                {
                    if (expr.OutValue is bool b)
                    {
                        if (b)
                        {
                            toReturn = GetUpToNextDirective(inInfo, ref outInfo);
                            break;
                        }
                    }
                    else
                    {
                        if (IsDirectiveDefined(currentDir))
                        {
                            toReturn = GetUpToNextDirective(inInfo, ref outInfo);
                            break;
                        }
                    }
                }
                // if still here - skip statements
                SkipUpToNextDirective();
                currentDir = ParseTopLevel(inInfo, ref outInfo) as AstDirectiveStmt;
            }
            while (currentDir.DirectiveType != DirectiveType.EndIf)
            {
                // if still here - skip statements
                SkipUpToNextDirective();
                currentDir = ParseTopLevel(inInfo, ref outInfo) as AstDirectiveStmt;
            }
            return toReturn;

            List<AstStatement> GetUpToNextDirective(ParserInInfo inInfo, ref ParserOutInfo outInfo)
            {
                List<AstStatement> toAdd = new List<AstStatement>();
                SkipNewlines();
                while (_lexer.PeekToken().Type != TokenType.SharpIdentifier)
                {
                    toAdd.Add(ParseTopLevel(inInfo, ref outInfo));
                    SkipNewlines();
                }
                return toAdd;
            }

            void SkipUpToNextDirective()
            {
                SkipNewlines();
                while (_lexer.PeekToken().Type != TokenType.SharpIdentifier)
                {
                    _lexer.SkipLine();
                }
            }

            bool IsDirectiveDefined(AstDirectiveStmt dir)
            {
                string nameDir = dir.RightPart is AstNestedExpr nst2 ? (nst2.RightPart as AstIdExpr).Name : (dir.RightPart as AstIdExpr).Name;

                Func<AstDirectiveStmt, bool> check = (a) =>
                {
                    string nameA = a.RightPart is AstNestedExpr nst ? (nst.RightPart as AstIdExpr).Name : (a.RightPart as AstIdExpr).Name;
                    return nameA == nameDir;
                };

                foreach (var a in file.Defines)
                {
                    if (check(a))
                        return true;
                }
                foreach (var a in _compiler.CurrentProjectData.Defines)
                {
                    if (a == nameDir)
                        return true;
                }
                return false;
            }
        }
    }
}
