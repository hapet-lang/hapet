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
                if (e is AstNestedExpr nest && nest.RightPart is AstIdExpr i)
                {
                    name = i;
                }
                else
                {
                    ReportMessage(e, [], ErrorCode.Get(CTEN.ArgumentNameNotIdent));
                }

                Consume(TokenType.Colon, ErrMsg(":", "after name in argument"));
                SkipNewlines();

                expr = ParseExpression(inInfo, ref outInfo) as AstExpression;
            }
            else
            {
                expr = e as AstExpression;
            }

            return new AstArgumentExpr(expr, name, new Location(beg, expr.Ending));
        }

        private List<AstArgumentExpr> ParseArgumentList(out TokenLocation beg, out TokenLocation end)
        {
            beg = Consume(TokenType.OpenParen, ErrMsg("(", "at beginning of argument list")).Location;

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
            bool isParams = false;
            bool isArglist = false;

            TokenLocation beg = null, end = null;

            // check for 'arglist'
            if (CheckToken(TokenType.KwArglist))
            {
                isArglist = true;
                var loc = NextToken().Location;
                beg = loc.Beginning;
                end = loc.Ending;
                return GetParam(); // just return it
            }

            // check for 'params'
            if (CheckToken(TokenType.KwParams))
            {
                isParams = true;
                NextToken();
            }

            // do not allow multiply here!!! read in desc - why!!!
            inInfo.AllowMultiplyExpression = false;
            var e = ParseExpression(inInfo, ref outInfo);
            inInfo.AllowMultiplyExpression = true;

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
            return GetParam();

            AstParamDecl GetParam()
            {
                // TODO: doc string???
                return new AstParamDecl(ptype as AstNestedExpr, pname, defaultValue, "", new Location(beg, end))
                {
                    IsParams = isParams,
                    IsArglist = isArglist,
                };
            }
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
            var list = ParseArgumentList(out var beg, out var end);

            //if (CheckToken(TokenType.Arrow))
            //{
            //    // if only one id is given for a parameter, then this should be used as name, not type
            //    foreach (var p in list)
            //    {
            //        if (p.Name == null && p.Type != null && p.Type is AstNestedExpr nest)
            //        {
            //            p.Name = nest.RightPart as AstIdExpr;
            //            if (p.Name == null)
            //                ReportMessage(p.Type.Location, [], ErrorCode.Get(CTEN.LambdaParamNameNotIdent));
            //            p.Type = null;
            //        }
            //    }
            //    return ParseLambdaDeclaration(list, beg, inInfo.AllowCommaForTuple);
            //}

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
                    if (list[0].Expr is AstNestedExpr)
                    {
                        var next = PeekToken();
                        // WARN: could be better checks?
                        var castNextToken = new TokenType[] { TokenType.OpenParen, TokenType.Identifier,
                            TokenType.NumberLiteral, TokenType.StringLiteral, TokenType.CharLiteral };
                        if (castNextToken.Contains(next.Type))
                        {
                            // probably a cast 
                            var expr = list[0].Expr;
                            expr.Location = new Location(beg, end);

                            var sub = ParsePostUnaryExpression(inInfo, ref outInfo);
                            var cst = new AstCastExpr(expr, sub as AstExpression, new Location(beg, sub.Ending));

                            // error if it is not an expr
                            if (sub is not AstExpression)
                            {
                                ReportMessage(sub, [], ErrorCode.Get(CTEN.CastSubNotExpr));
                            }
                            return cst;
                        }
                        else
                        {
                            // probably just smth like
                            // a = (b) + (c)
                            return list[0].Expr;
                        }
                    }
                    else
                    {
                        // just a more priority for expr
                        // like '(a & b) == 0'
                        return list[0].Expr;
                    }
                }
            }

            return new AstTupleExpr(list.Select(x => x.Expr as AstNestedExpr).ToList(), new Location(beg, end));
        }
    }
}
