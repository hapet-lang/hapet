using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using System.Diagnostics;
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
                case "undef": type = DirectiveType.Undef; break;

                case "error": type = DirectiveType.Error; break;
                case "warning": type = DirectiveType.Warning; break;
            }

            switch (type) 
            {
                case DirectiveType.Error:
                case DirectiveType.Warning:
                case DirectiveType.MetadataFile:
                    {
                        var expr = ParseExpression(inInfo, ref outInfo) as AstExpression;
                        if (expr is not AstStringExpr)
                        {
                            // error here
                            ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.CommonStringExpected));
                        }
                        return new AstDirectiveStmt(null, type, new Location(tkn.Location, expr.Location.Ending)) { Value = expr };
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
                        var expr = ParseExpression(inInfo, ref outInfo) as AstExpression;
                        return new AstDirectiveStmt(null, type, new Location(tkn.Location, expr.Location.Ending)) { Value = expr };
                    }
                case DirectiveType.Define:
                case DirectiveType.Undef:
                    {
                        var expr = ParseExpression(inInfo, ref outInfo);
                        if (!(expr is AstNestedExpr nst && nst.RightPart is AstIdExpr idExpr))
                        {
                            // error here
                            ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                            return new AstEmptyStmt();
                        }

                        AstExpression value = null;
                        if (!CheckToken(TokenType.NewLine))
                        {
                            value = ParseExpression(inInfo, ref outInfo) as AstExpression;
                        }

                        return new AstDirectiveStmt(idExpr, type, new Location(tkn.Location, expr.Location.Ending)) { Value = value };
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

                if (IsDirectiveDefined(currentDir))
                {
                    toReturn = GetUpToNextDirective(inInfo, ref outInfo);
                    break;
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
                InferenceDirectiveValue(dir.Value, file);
                return dir.Value.OutValue is bool b ? b : false;
            }
        }

        private void InferenceDirectiveValue(AstExpression expr, ProgramFile file)
        {
            if (expr is AstNestedExpr nst)
            {
                Debug.Assert(nst.LeftPart == null); // why not null here? should be null
                InferenceDirectiveValue(nst.RightPart, file);
                nst.OutValue = nst.RightPart.OutValue;
                return;
            }
            else if (expr is AstIdExpr id)
            {
                // just an id, so search for the same define names
                foreach (var a in file.Defines)
                {
                    if (a.RightPart?.Name == id.Name && a.Value == null)
                    {
                        id.OutValue = true;
                        return;
                    }
                    else if (a.RightPart?.Name == id.Name && a.Value != null)
                    {
                        InferenceDirectiveValue(a.Value, file);
                        id.OutValue = a.Value.OutValue;
                        return;
                    }
                }
                foreach (var a in _compiler.CurrentProjectData.Defines)
                {
                    if (a.Key == id.Name && string.IsNullOrWhiteSpace(a.Value))
                    {
                        id.OutValue = true;
                        return;
                    }
                    else if (a.Key == id.Name && !string.IsNullOrWhiteSpace(a.Value))
                    {
                        id.OutValue = a.Value;
                        return;
                    }
                }
                id.OutValue = false; // not found
            }
            else if (expr is AstBinaryExpr bin)
            {
                bin.OutValue = false;

                InferenceDirectiveValue(bin.Right, file);
                InferenceDirectiveValue(bin.Left, file);
                if (bin.Left.OutValue is string s1 && bin.Right.OutValue is string s2)
                {
                    if (bin.Operator == "==")
                        bin.OutValue = s1 == s2;
                    else if (bin.Operator == "!=")
                        bin.OutValue = s1 != s2;
                    else
                    {
                        // expect to versions comparisons
                        var p1 = Version.TryParse(s1, out Version ver1);
                        var p2 = Version.TryParse(s2, out Version ver2);
                        if (p1 && p2)
                        {
                            if (bin.Operator == ">")
                                bin.OutValue = ver1 > ver2;
                            else if (bin.Operator == "<")
                                bin.OutValue = ver1 < ver2;
                            else if (bin.Operator == ">=")
                                bin.OutValue = ver1 >= ver2;
                            else if (bin.Operator == "<=")
                                bin.OutValue = ver1 <= ver2;
                        }
                    }
                }
                else if (bin.Left.OutValue is int i1 && bin.Right.OutValue is int i2)
                {
                    if (bin.Operator == "==")
                        bin.OutValue = i1 == i2;
                    else if (bin.Operator == "!=")
                        bin.OutValue = i1 != i2;
                    else if (bin.Operator == ">")
                        bin.OutValue = i1 > i2;
                    else if (bin.Operator == "<")
                        bin.OutValue = i1 < i2;
                    else if (bin.Operator == ">=")
                        bin.OutValue = i1 >= i2;
                    else if (bin.Operator == "<=")
                        bin.OutValue = i1 <= i2;
                }
                else if (bin.Left.OutValue is bool b1 && bin.Right.OutValue is bool b2)
                {
                    if (bin.Operator == "==")
                        bin.OutValue = b1 == b2;
                    else if (bin.Operator == "!=")
                        bin.OutValue = b1 != b2;
                    else if (bin.Operator == "||")
                        bin.OutValue = b1 || b2;
                    else if (bin.Operator == "&&")
                        bin.OutValue = b1 && b2;
                }
            }
            else if (expr is AstUnaryExpr un)
            {
                un.OutValue = false;

                InferenceDirectiveValue(un.SubExpr, file);
                if (un.SubExpr.OutValue is bool b1 && un.Operator == "!")
                {
                    un.OutValue = !b1;
                }
            }
        }
    }
}
