using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using System.Runtime;
using System.Xml.Linq;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstArgumentExpr ParseArgument(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            TokenLocation beg;
            AstExpression expr;
            AstIdExpr name = null;
            ParameterModificator argModificator = ParameterModificator.None;

            // check for 'ref'
            if (CheckToken(TokenType.KwRef))
            {
                argModificator = ParameterModificator.Ref;
                NextToken();
            }
            // check for 'out'
            else if (CheckToken(TokenType.KwOut))
            {
                argModificator = ParameterModificator.Out;
                NextToken();
            }

            // allow multiply in args
            var savedAllowMul = inInfo.AllowMultiplyExpression;
            inInfo.AllowMultiplyExpression = true;

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

            inInfo.AllowMultiplyExpression = savedAllowMul;

            return new AstArgumentExpr(expr, name, new Location(beg, expr.Ending))
            {
                ArgumentModificator = argModificator,
            };
        }

        private List<AstArgumentExpr> ParseArgumentList(ParserInInfo inInfo, ref ParserOutInfo outInfo, out TokenLocation beg, out TokenLocation end)
        {
            beg = Consume(TokenType.OpenParen, ErrMsg("(", "at beginning of argument list")).Location;

            SkipNewlines();
            var args = new List<AstArgumentExpr>();
            while (true)
            {
                var next = PeekToken();
                if (next.Type == TokenType.CloseParen || next.Type == TokenType.EOF)
                    break;
                args.Add(ParseArgument(inInfo, ref outInfo));

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

        private AstParamDecl ParseParameter(ParserInInfo inInfo, ref ParserOutInfo outInfo, bool allowDefaultValue = true)
        {
            AstIdExpr pname = null;
            AstStatement ptype = null;
            AstExpression defaultValue = null;
            ParameterModificator parModificator = ParameterModificator.None;

            TokenLocation beg = null, end = null;

            // check for 'arglist'
            if (CheckToken(TokenType.KwArglist))
            {
                parModificator = ParameterModificator.Arglist;
                var loc = NextToken().Location;
                beg = loc.Beginning;
                end = loc.Ending;
                return GetParam(); // just return it
            }
            // check for 'params'
            else if (CheckToken(TokenType.KwParams))
            {
                parModificator = ParameterModificator.Params;
                NextToken();
            }
            // check for 'ref'
            else if (CheckToken(TokenType.KwRef))
            {
                parModificator = ParameterModificator.Ref;
                NextToken();
            }
            // check for 'out'
            else if (CheckToken(TokenType.KwOut))
            {
                parModificator = ParameterModificator.Out;
                NextToken();
            }

            // do not allow multiply here!!! read in desc - why!!!
            var savedAllowMul = inInfo.AllowMultiplyExpression;
            inInfo.AllowMultiplyExpression = false;
            var e = ParseExpression(inInfo, ref outInfo);
            inInfo.AllowMultiplyExpression = savedAllowMul;

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
                return new AstParamDecl(ptype as AstNestedExpr, pname, defaultValue, "", new Location(beg, end))
                {
                    ParameterModificator = parModificator,
                };
            }
        }

        private List<AstParamDecl> ParseParameterList(ParserInInfo inInfo, ref ParserOutInfo outInfo, TokenType open, TokenType close, out TokenLocation beg, out TokenLocation end, bool allowDefaultValue = true)
        {
            var parameters = new List<AstParamDecl>();

            beg = Consume(open, ErrMsg("(/[", "at beginning of parameter list")).Location;
            SkipNewlines();

            while (true)
            {
                var next = PeekToken();
                if (next.Type == close || next.Type == TokenType.EOF)
                    break;

                var a = ParseParameter(inInfo, ref outInfo, allowDefaultValue);
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
            var saved1 = inInfo.IsInTupleParsing;
            inInfo.IsInTupleParsing = true;

            // expecting (int, int b) here
            if (inInfo.AllowTypedTuple)
            {
                var list2 = ParseParameterList(inInfo, ref outInfo, TokenType.OpenParen, TokenType.CloseParen, out var beg2, out var end2, false);
                if (list2.Count == 1)
                {
                    if (list2[0].Type is AstNestedExpr)
                    {
                        OnExit();
                        return HandleOneElement(list2[0].Type, beg2, end2, ref outInfo);
                    }
                    else
                    {
                        OnExit();
                        // just a more priority for expr
                        // like '(a & b) == 0'
                        if (list2[0].Type is AstNestedExpr)
                            return list2[0].Type;
                        return new AstNestedExpr(list2[0].Type, null, list2[0].Type.Location);
                    }
                }

                OnExit();

                var tpl = new AstTupleExpr(list2.Select(x => x.Type).ToList(), new Location(beg2, end2));
                tpl.Names = list2.Select(x => x.Name).ToList();
                tpl.IsTypedTuple = true;
                return new AstNestedExpr(tpl, null, tpl.Location);
            }

            // need to lookahead for =>
            bool isLambda;
            UpdateLookAheadLocation();
            int tmpParenCounter = 1;
            NextLookAhead();
            while (tmpParenCounter != 0)
            {
                var curr = NextLookAhead();
                if (curr.Type == TokenType.OpenParen)
                    tmpParenCounter++;
                else if (curr.Type == TokenType.CloseParen)
                    tmpParenCounter--;
            }
            isLambda = NextLookAhead().Type == TokenType.Arrow;
            if (isLambda)
                return ParseLambdaDecl(inInfo, ref outInfo);

            var list = ParseArgumentList(inInfo, ref outInfo, out var beg, out var end);

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
                        OnExit();
                        return HandleOneElement(list[0].Expr, beg, end, ref outInfo);
                    }
                    else
                    {
                        OnExit();
                        // just a more priority for expr
                        // like '(a & b) == 0'
                        if (list[0].Expr is AstNestedExpr)
                            return list[0].Expr;
                        return new AstNestedExpr(list[0].Expr, null, list[0].Expr.Location);
                    }
                }
            }

            OnExit();

            var tpl2 = new AstTupleExpr(list.Select(x => x.Expr).ToList(), new Location(beg, end));
            tpl2.Names = list.Select(x => x.Name).ToList();
            tpl2.IsTypedTuple = false;
            return new AstNestedExpr(tpl2, null, tpl2.Location);

            AstExpression HandleOneElement(AstExpression element, TokenLocation beg, TokenLocation end, ref ParserOutInfo outInfo)
            {
                var next = PeekToken();
                // WARN: could be better checks?
                var castNextToken = new TokenType[] 
                { 
                    TokenType.OpenParen, TokenType.Identifier,
                    TokenType.NumberLiteral, TokenType.StringLiteral, 
                    TokenType.CharLiteral, TokenType.KwSizeof,
                    TokenType.KwTypeof, TokenType.KwAlignof,
                    TokenType.KwNameof
                };
                if (castNextToken.Contains(next.Type))
                {
                    // probably a cast 
                    var expr = element;
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
                    if (element is AstNestedExpr)
                        return element;
                    return new AstNestedExpr(element, null, element.Location);
                }
            }

            void OnExit()
            {
                inInfo.IsInTupleParsing = saved1;
            }
        }

        private AstStatement ParseLambdaDecl(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            TokenLocation beg = null;
            List<AstParamDecl> paramss = new List<AstParamDecl>();
            if (CheckToken(TokenType.OpenParen))
            {
                paramss.AddRange(ParseParameterList(inInfo, ref outInfo, TokenType.OpenParen, TokenType.CloseParen, out beg, out var _, true));
            }
            // else - just identifier
            else
            {
                // when x => x... supported
                var id = ParseIdentifierExpression(inInfo, allowDots: false, allowGenerics: false, expectIdent: true);
                if (id.RightPart is not AstIdExpr)
                {
                    ReportMessage(id.RightPart.Location, [], ErrorCode.Get(CTEN.ParameterNameNotIdent));
                }
                paramss.Add(new AstParamDecl(null, id.RightPart as AstIdExpr, null, "", id.RightPart.Location));
                beg = id.Beginning;
            }

            // handle params like 'a' not 'int a' - need to swap Name and Type here
            foreach (var par in paramss)
            {
                if (par.Name != null)
                    continue;
                par.Name = (par.Type as AstNestedExpr).RightPart as AstIdExpr;
                par.Type = null;
            }

            SkipNewlines();
            Consume(TokenType.Arrow, ErrMsg("=>", "before lambda block"));
            SkipNewlines();

            AstBlockExpr body;
            if (CheckToken(TokenType.OpenBrace))
            {
                body = ParseBlockExpression(inInfo, ref outInfo);
            }
            else
            {
                // getting only one stmt if there are no braces
                var onlyStmt = ParseStatement(inInfo, ref outInfo);
                var weakReturnStmt = new AstReturnStmt(null, onlyStmt.Location)
                {
                    IsWeakReturn = true,
                    WeakReturnStatement = onlyStmt,
                };
                body = new AstBlockExpr(new List<AstStatement>() { weakReturnStmt }, onlyStmt);
            }
            var lambda = new AstLambdaExpr(paramss, body, null, new Location(beg, body.Ending));
            _compiler.LambdasAndNested.Add(lambda);
            return lambda;
        }

        private AstUnknownDecl PrepareTupleExpr(AstNestedExpr nst, AstTupleExpr tpl, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            AstUnknownDecl decl;
            if (tpl.IsFullyNamed)
            {
                // (int a, int b) = (3, 4);
                decl = new AstUnknownDecl(nst, null, nst);
            }
            else
            {
                // expect the name
                var name = ParseIdentifierExpression(inInfo, allowDots: false, allowGenerics: false, allowTupled: true);
                decl = new AstUnknownDecl(nst, name.RightPart as AstIdExpr, nst);
            }
            return decl;
        }
    }
}
