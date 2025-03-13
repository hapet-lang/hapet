using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstArgumentExpr ParseArgument()
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            TokenLocation beg;
            AstExpression expr;
            AstIdExpr name = null;

            var e = ParseExpression(inInfo, ref outInfo);
            beg = e.Beginning;

            // if next token is : then e is the name of the parameter
            if (CheckToken(TokenType.Colon))
            {
                if (e is AstIdExpr i)
                {
                    name = i;
                }
                else
                {
                    ReportMessage(e, [], ErrorCode.Get(CTEN.ArgumentNameNotIdent));
                }

                Consume(TokenType.Equal, ErrMsg(":", "after name in argument"));
                SkipNewlines();

                expr = ParseExpression(inInfo, ref outInfo) as AstExpression;
            }
            else
            {
                expr = e as AstExpression;
            }

            return new AstArgumentExpr(expr, name, new Location(beg, expr.Ending));
        }

        private List<AstArgumentExpr> ParseArgumentList(out TokenLocation end)
        {
            Consume(TokenType.OpenParen, ErrMsg("(", "at beginning of argument list"));

            SkipNewlines();
            var args = new List<AstArgumentExpr>();
            while (true)
            {
                var next = PeekToken();
                if (next.Type == TokenType.CloseParen || next.Type == TokenType.EOF)
                    break;
                args.Add(ParseArgument());

                next = PeekToken();
                if (next.Type == TokenType.NewLine)
                {
                    NextToken();
                }
                else if (next.Type == TokenType.Comma)
                {
                    NextToken();
                    SkipNewlines();
                }
                else if (next.Type == TokenType.CloseParen)
                    break;
                else
                {
                    NextToken();
                    ReportMessage(next.Location, [], ErrorCode.Get(CTEN.FailedToParseArguments));
                }
            }
            end = Consume(TokenType.CloseParen, ErrMsg(")", "at end of argument list")).Location;

            return args;
        }

        private AstParamDecl ParseParameter(bool allowDefaultValue = true)
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            AstIdExpr pname = null;
            AstStatement ptype = null;
            AstExpression defaultValue = null;

            TokenLocation beg = null, end = null;

            inInfo.AllowPointerExpression = true;
            var e = ParseExpression(inInfo, ref outInfo);
            inInfo.AllowPointerExpression = false;

            beg = e.Beginning;
            SkipNewlines();

            if (e is AstUnknownDecl udecl)
            {
                pname = udecl.Name as AstIdExpr;
                ptype = udecl.Type;
            }
            else
            {
                // if next token is ident then e is the type of the parameter
                if (CheckToken(TokenType.Identifier))
                {
                    SkipNewlines();

                    var probName = ParseExpression(inInfo, ref outInfo);
                    if (probName is not AstIdExpr)
                    {
                        ReportMessage(probName.Location, [], ErrorCode.Get(CTEN.ParameterNameNotIdent));
                    }
                    pname = probName as AstIdExpr;
                    ptype = e;
                }
                else
                {
                    ptype = e;
                }
            }

            // ptype is only a NestedShite
            if (ptype is not AstNestedExpr)
                ptype = new AstNestedExpr(ptype as AstExpression, null, ptype);

            end = ptype.Ending;

            if (allowDefaultValue)
            {
                // optional default value
                SkipNewlines();
                if (CheckToken(TokenType.Equal))
                {
                    NextToken();
                    SkipNewlines();
                    var probDefVal = ParseExpression(inInfo, ref outInfo);
                    if (probDefVal is not AstExpression)
                    {
                        ReportMessage(probDefVal.Location, [], ErrorCode.Get(CTEN.ParamDefaultNotExpr));
                    }
                    defaultValue = probDefVal as AstExpression;
                    end = defaultValue.Ending;
                }
            }

            // TODO: doc string???
            return new AstParamDecl(ptype as AstNestedExpr, pname, defaultValue, "", new Location(beg, end));
        }

        private List<AstParamDecl> ParseParameterList(TokenType open, TokenType close, out TokenLocation beg, out TokenLocation end, bool allowDefaultValue = true)
        {
            var parameters = new List<AstParamDecl>();

            beg = Consume(open, ErrMsg("(/[", "at beginning of parameter list")).Location;
            SkipNewlines();

            while (true)
            {
                var next = PeekToken();
                if (next.Type == close || next.Type == TokenType.EOF)
                    break;

                var a = ParseParameter(allowDefaultValue);
                parameters.Add(a);

                SkipNewlines();
                next = PeekToken();
                if (next.Type == TokenType.Comma)
                {
                    NextToken();
                    SkipNewlines();
                }
                else if (next.Type == close)
                    break;
                else
                {
                    NextToken();
                    SkipNewlines();
                    ReportMessage(next.Location, [next.ToString()], ErrorCode.Get(CTEN.FailedToParseParameters));
                }
            }

            end = Consume(close, ErrMsg(")/]", "at end of parameter list")).Location;

            return parameters;
        }

        private AstStatement ParseTupleExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var list = ParseParameterList(TokenType.OpenParen, TokenType.CloseParen, out var beg, out var end, allowDefaultValue: true);

            SkipNewlines();

            // function expression
            // hash identifier for directives
            if (inInfo.AllowFunctionDeclaration && CheckTokens(TokenType.OpenBrace, TokenType.Semicolon, TokenType.Colon))
            {
                return ParseFuncDeclaration(list, new Location(beg, end), inInfo, ref outInfo);
            }

            if (CheckToken(TokenType.Arrow))
            {
                // if only one id is given for a parameter, then this should be used as name, not type
                foreach (var p in list)
                {
                    if (p.Name == null && p.Type != null)
                    {
                        p.Name = p.Type.RightPart as AstIdExpr;
                        if (p.Name == null)
                            ReportMessage(p.Type.Location, [], ErrorCode.Get(CTEN.LambdaParamNameNotIdent));
                        p.Type = null;
                    }
                }
                return ParseLambdaDeclaration(list, beg, inInfo.AllowCommaForTuple);
            }

            bool isType = false;
            foreach (var v in list)
            {
                if (v.Name != null)
                    isType = true;
            }

            if (!isType)
            {
                if (list.Count == 1)
                {
                    if (list[0].Type is AstNestedExpr)
                    {
                        var next = PeekToken();
                        // WARN: could be better checks?
                        var castNextToken = new TokenType[] { TokenType.OpenParen, TokenType.Identifier,
                            TokenType.NumberLiteral, TokenType.StringLiteral, TokenType.CharLiteral };
                        if (castNextToken.Contains(next.Type))
                        {
                            // probably a cast 
                            var expr = list[0].Type;
                            expr.Location = new Location(beg, end);

                            var savedAllowFuncs = inInfo.AllowFunctionDeclaration;
                            inInfo.AllowFunctionDeclaration = false;
                            var sub = ParsePostUnaryExpression(inInfo, ref outInfo);
                            inInfo.AllowFunctionDeclaration = savedAllowFuncs;

                            var cst = new AstCastExpr(expr, sub, new Location(beg, sub.Ending));

                            // error if it is not an expr
                            if (cst.SubExpression is not AstExpression)
                            {
                                ReportMessage(cst.SubExpression, [], ErrorCode.Get(CTEN.CastSubNotExpr));
                            }
                            // error if it is not an expr
                            if (cst.TypeExpr is not AstExpression)
                            {
                                ReportMessage(cst.TypeExpr, [], ErrorCode.Get(CTEN.CastTargetNotExpr));
                            }
                            return cst;
                        }
                        else
                        {
                            // probably just smth like
                            // a = (b) + (c)
                            return list[0].Type;
                        }
                    }
                    else
                    {
                        // just a more priority for expr
                        // like '(a & b) == 0'
                        return list[0].Type;
                    }
                }
            }

            return new AstTupleExpr(list, new Location(beg, end));
        }
    }
}
