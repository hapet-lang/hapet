using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using System.Diagnostics;
using System.Security.Cryptography;
using static System.Net.Mime.MediaTypeNames;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var savedMessage = inInfo.Message;
            inInfo.Message ??= new MessageResolver() { XmlMessage = ErrorCode.Get(CTEN.CommonUnexpectedInExpr) };

            var expr = ParseOrExpression(inInfo, ref outInfo);

            inInfo.Message = savedMessage;

            return expr;
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseOrExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseAndExpression, inInfo, ref outInfo,
                (TokenType.LogicalOr, "||"));
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseAndExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseIsExpression, inInfo, ref outInfo,
                (TokenType.LogicalAnd, "&&"));
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseIsExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var lhs = ParseAsExpression(inInfo, ref outInfo);
            AstStatement rhs;

            while (CheckToken(TokenType.KwIs))
            {
                var _ = NextToken();
                SkipNewlines();
                rhs = ParseAsExpression(inInfo, ref outInfo);
                var binExpr = new AstBinaryExpr("is", lhs as AstExpression, rhs as AstExpression, new Location(lhs.Beginning, rhs.Ending));

                // error if it is not an expr
                if (lhs is not AstExpression)
                    ReportMessage(lhs, [binExpr.Operator], ErrorCode.Get(CTEN.ExprsExpectedInBinExpr));
                // error if it is not an expr
                if (rhs is not AstExpression)
                    ReportMessage(rhs, [binExpr.Operator], ErrorCode.Get(CTEN.ExprsExpectedInBinExprR));

                // set to return it
                lhs = binExpr;
            }
            return lhs;
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseAsExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var lhs = ParseInExpression(inInfo, ref outInfo);
            AstStatement rhs;

            while (CheckToken(TokenType.KwAs))
            {
                var _ = NextToken();
                SkipNewlines();
                rhs = ParseInExpression(inInfo, ref outInfo);
                var binExpr = new AstBinaryExpr("as", lhs as AstExpression, rhs as AstExpression, new Location(lhs.Beginning, rhs.Ending));

                // error if it is not an expr
                if (lhs is not AstExpression)
                    ReportMessage(lhs, [binExpr.Operator], ErrorCode.Get(CTEN.ExprsExpectedInBinExpr));
                // error if it is not an expr
                if (rhs is not AstExpression)
                    ReportMessage(rhs, [binExpr.Operator], ErrorCode.Get(CTEN.ExprsExpectedInBinExprR));

                // set to return it
                lhs = binExpr;
            }
            return lhs;
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseInExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var lhs = ParseComparisonExpression(inInfo, ref outInfo);
            AstStatement rhs;

            while (CheckToken(TokenType.KwIn))
            {
                var _ = NextToken();
                SkipNewlines();
                rhs = ParseComparisonExpression(inInfo, ref outInfo);
                var binExpr = new AstBinaryExpr("in", lhs as AstExpression, rhs as AstExpression, new Location(lhs.Beginning, rhs.Ending));

                // error if it is not an expr
                if (lhs is not AstExpression)
                    ReportMessage(lhs, [binExpr.Operator], ErrorCode.Get(CTEN.ExprsExpectedInBinExpr));
                // error if it is not an expr
                if (rhs is not AstExpression)
                    ReportMessage(rhs, [binExpr.Operator], ErrorCode.Get(CTEN.ExprsExpectedInBinExprR));

                // set to return it
                lhs = binExpr;
            }
            return lhs;
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseComparisonExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseBitAndOrExpression, inInfo, ref outInfo,
                (TokenType.Less, "<"),
                (TokenType.LessEqual, "<="),
                (TokenType.Greater, ">"),
                (TokenType.GreaterEqual, ">="),
                (TokenType.DoubleEqual, "=="),
                (TokenType.NotEqual, "!="));
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseBitAndOrExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseBitShiftExpression, inInfo, ref outInfo,
                (TokenType.Ampersand, "&"),
                (TokenType.VerticalSlash, "|"));
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseBitShiftExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseAddSubExpression, inInfo, ref outInfo,
                (TokenType.LessLess, "<<"),
                (TokenType.GreaterGreater, ">>"));
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseAddSubExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseMulDivExpression, inInfo, ref outInfo,
                (TokenType.Plus, "+"),
                (TokenType.Minus, "-"));
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseMulDivExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseUnaryExpression, inInfo, ref outInfo,
                (TokenType.Asterisk, "*"),
                (TokenType.ForwardSlash, "/"),
                (TokenType.Percent, "%"));
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseBinaryLeftAssociativeExpression(
            ExpressionParser sub,
            ParserInInfo inInfo, ref ParserOutInfo outInfo,
            params (TokenType, string)[] types)
        {
            return ParseLeftAssociativeExpression(sub, inInfo, ref outInfo, type =>
            {
                foreach (var (t, o) in types)
                {
                    if (t == type)
                        return o;
                }
                return null;
            });
        }

        private AstStatement ParseLeftAssociativeExpression(
            ExpressionParser sub,
            ParserInInfo inInfo, ref ParserOutInfo outInfo,
            Func<TokenType, string> tokenMapping)
        {
            var lhs = sub(inInfo, ref outInfo);
            AstStatement rhs = null;

            while (true)
            {
                var next = PeekToken();

                // cringe handle >>
                // https://github.com/dotnet/roslyn/blob/62646c22f6bd7b213e7e15dbc0dfadfe47a1e30f/src/Compilers/CSharp/Portable/Parser/Lexer.cs#L4118-L4122
                // https://github.com/dotnet/roslyn/blob/62646c22f6bd7b213e7e15dbc0dfadfe47a1e30f/src/Compilers/CSharp/Portable/Parser/LanguageParser.cs#L11067-L11073
                UpdateLookAheadLocation();
                var t1 = PeekLookAhead();
                NextLookAhead();
                var t2 = PeekLookAhead();
                if (t1.Type == TokenType.Greater && t2.Type == TokenType.Greater)
                {
                    next.Type = TokenType.GreaterGreater;
                    next.Location.End = t2.Location.End;
                }

                var op = tokenMapping(next.Type);
                if (op == null)
                {
                    return lhs;
                }

                // make one more token eat because of pseudo >>
                if (next.Type == TokenType.GreaterGreater)
                    NextToken();

                NextToken();
                SkipNewlines();
                rhs = sub(inInfo, ref outInfo);
                var binExpr = new AstBinaryExpr(op, lhs as AstExpression, rhs as AstExpression, new Location(lhs.Beginning, rhs.Ending));

                // error if it is not an expr
                if (lhs is not AstExpression)
                    ReportMessage(lhs, [binExpr.Operator], ErrorCode.Get(CTEN.ExprsExpectedInBinExpr));
                // error if it is not an expr
                if (rhs is not AstExpression)
                    ReportMessage(rhs, [binExpr.Operator], ErrorCode.Get(CTEN.ExprsExpectedInBinExprR));

                lhs = binExpr;
            }
        }

        private AstStatement ParseUnaryExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var next = PeekToken();
            if (next.Type == TokenType.Ampersand)
            {
                NextToken();
                SkipNewlines();

                var sub = ParseUnaryExpression(inInfo, ref outInfo);
                if (sub is not AstExpression expr)
                {
                    ReportMessage(sub, ["&"], ErrorCode.Get(CTEN.ExprExpectedInUnExpr));
                    return sub;
                }
                return new AstAddressOfExpr(expr, new Location(next.Location, sub.Ending));
            }
            else if (next.Type == TokenType.Asterisk)
            {
                NextToken();
                SkipNewlines();
                var sub = ParseUnaryExpression(inInfo, ref outInfo);
                if (sub is not AstExpression expr)
                {
                    ReportMessage(sub, ["*"], ErrorCode.Get(CTEN.ExprExpectedInUnExpr));
                    return sub;
                }
                return new AstPointerExpr(expr, true, new Location(next.Location, sub.Ending));
            }
            else if (next.Type == TokenType.Minus || next.Type == TokenType.Plus)
            {
                NextToken();
                SkipNewlines();
                var sub = ParseUnaryExpression(inInfo, ref outInfo);
                string op = "";
                switch (next.Type)
                {
                    case TokenType.Plus: op = "+"; break;
                    case TokenType.Minus: op = "-"; break;
                }
                var un = new AstUnaryExpr(op, sub as AstExpression, new Location(next.Location, sub.Ending));
                // error if it is not an expr
                if (sub is not AstExpression)
                {
                    ReportMessage(sub, [un.Operator], ErrorCode.Get(CTEN.ExprExpectedInUnExpr));
                }
                return un;
            }
            else if (next.Type == TokenType.Bang)
            {
                NextToken();
                SkipNewlines();
                var sub = ParseUnaryExpression(inInfo, ref outInfo);
                var un = new AstUnaryExpr("!", sub as AstExpression, new Location(next.Location, sub.Ending));
                // error if it is not an expr
                if (sub is not AstExpression)
                {
                    ReportMessage(sub, [un.Operator], ErrorCode.Get(CTEN.ExprExpectedInUnExpr));
                }
                return un;
            }
            else if (next.Type == TokenType.PlusPlus || next.Type == TokenType.MinusMinus)
            {
                NextToken();
                SkipNewlines();
                var sub = ParseUnaryExpression(inInfo, ref outInfo);
                var op = next.Type == TokenType.PlusPlus ? "++" : "--";
                var un = new AstUnaryIncDecExpr(op, sub as AstExpression, new Location(next.Location, sub.Ending)) { IsPrefix = true };
                // error if it is not a nested!!!
                if (sub is not AstNestedExpr)
                {
                    ReportMessage(sub, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                }
                return un;
            }

            return ParsePostUnaryExpression(inInfo, ref outInfo);
        }

        private AstStatement ParsePostUnaryExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var expr = ParseAtomicExpression(inInfo, ref outInfo);

            // just move forward the udecl :)
            if (expr is AstUnknownDecl)
                return expr;

            bool breakLoop = false;
            while (!breakLoop)
            {
                switch (PeekToken().Type)
                {
                    case TokenType.OpenParen:
                        {
                            // TODO: not only nested should be allowed. tuples, lamdas and other shite
                            var args = ParseArgumentList(out var _, out var end);
                            if (expr is not AstNestedExpr nestExpr)
                            {
                                ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.CallTargetExprExpected));
                                return expr;
                            }
                            if (nestExpr.RightPart is not AstIdExpr idExpr)
                            {
                                ReportMessage(nestExpr.Location, [], ErrorCode.Get(CTEN.CallNameIdentExpected));
                                return expr;
                            }

                            bool dotsAfter = CheckToken(TokenType.Period);

                            var callExpr = new AstCallExpr(nestExpr.LeftPart, idExpr.GetCopy(), args, new Location(expr.Beginning, end));
                            expr = new AstNestedExpr(callExpr, null, callExpr);

                            // check for dots after this!!! there could be a.asd().asd().ddd().d.lll()
                            // for better understand imagine we have 'anime.Asd().dwd.Lmao();'
                            // and this is the first entry. So we already parsed here 'anime.Asd()' 
                            // and need to check the rest shite
                            if (dotsAfter)
                            {
                                NextToken();
                                // here we are getting the rest 'dwd.Lmao'
                                expr = ParseIdentifierExpression(iniNested: expr as AstNestedExpr);
                                // so after this the upper loop will check if there is a OpenParent and so on
                                // if there is no OpenParen - then just NestedExpr will be returned
                            }
                        }
                        break;

                    case TokenType.OpenBracket:
                        {
                            NextToken();
                            SkipNewlines();

                            var args = new List<AstStatement>();
                            while (true)
                            {
                                var next = PeekToken();
                                if (next.Type == TokenType.CloseBracket || next.Type == TokenType.EOF)
                                    break;

                                args.Add(ParseExpression(inInfo, ref outInfo));
                                SkipNewlines();

                                next = PeekToken();
                                if (next.Type == TokenType.Comma)
                                {
                                    NextToken();
                                    SkipNewlines();
                                }
                                else if (next.Type == TokenType.CloseBracket)
                                    break;
                                else
                                {
                                    NextToken();
                                    ReportMessage(next.Location, [], ErrorCode.Get(CTEN.ArrayAccUnexpectedToken));
                                }
                            }
                            var end = Consume(TokenType.CloseBracket, ErrMsg("]", "at end of [..] operator")).Location;
                            if (args.Count == 0)
                            {
                                ReportMessage(end, [], ErrorCode.Get(CTEN.ArrayAccNoArgs));
                                args.Add(ParseEmptyExpression());
                            }
                            else if (args.Count > 1)
                            {
                                // TODO: mb allow them multiple args in []?
                                ReportMessage(end, [], ErrorCode.Get(CTEN.ArrayAccTooManyArgs));
                            }

                            var arrAcc = new AstArrayAccessExpr(expr as AstExpression, args[0] as AstExpression, new Location(expr.Beginning, end));
                            expr = new AstNestedExpr(arrAcc, null, arrAcc);

                            // check for dots after this!!! there could be a.arr[i].Length
                            // for better understand imagine we have 'a.arr[i].Length;'
                            // and this is the first entry. So we already parsed here 'a.arr[i]' 
                            // and need to check the rest shite
                            if (CheckToken(TokenType.Period))
                            {
                                NextToken();
                                // here we are getting the rest '.Length'
                                expr = ParseIdentifierExpression(iniNested: expr as AstNestedExpr);
                                // so after this the upper loop will check if there is a OpenParent and so on
                                // if there is no OpenParen - then just NestedExpr will be returned
                            }
                        }
                        break;

                    case TokenType.PlusPlus:
                    case TokenType.MinusMinus:
                        {
                            var tkn = NextToken();
                            SkipNewlines();
                            var op = tkn.Type == TokenType.PlusPlus ? "++" : "--";
                            var un = new AstUnaryIncDecExpr(op, expr as AstExpression, new Location(expr.Beginning, tkn.Location)) { IsPrefix = false };
                            // error if it is not a nested!!!
                            if (expr is not AstNestedExpr)
                            {
                                ReportMessage(expr, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                            }
                            return un;
                        }

                    default:
                        return expr;
                }
            }
            return expr;
        }

        private AstStatement ParseAtomicExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var token = PeekToken();
            switch (token.Type)
            {
                case TokenType.KwDefault:
                    {
                        NextToken();

                        // only type is expected
                        AstExpression typeExpr = null;
                        if (CheckToken(TokenType.OpenParen))
                        {
                            NextToken();
                            var saved = inInfo.AllowMultiplyExpression;
                            inInfo.AllowMultiplyExpression = false;
                            typeExpr = ParseAtomicExpression(inInfo, ref outInfo) as AstExpression;
                            inInfo.AllowMultiplyExpression = saved;
                            Consume(TokenType.CloseParen, ErrMsg(")", "after default' type arg"));
                        }
                        // it is just a 'default' word
                        return new AstDefaultExpr(new Location(token.Location)) { TypeForDefault = typeExpr };
                    }

                case TokenType.KwNull:
                    NextToken();
                    return new AstNullExpr(PointerType.NullLiteralType, new Location(token.Location));

                case TokenType.KwNew:
                    {
                        return ParseNewExpression();
                    }

                case TokenType.KwBase:
                    {
                        // this is when calling base func from child class like:
                        // public override void Anime()
                        // {
                        //     base.Anime();
                        // }
                        NextToken();
                        Consume(TokenType.Period, ErrMsg(".", "after 'base' word"));
                        return ParseIdentifierExpression(iniNested: new AstNestedExpr(new AstIdExpr("base", CurrentToken.Location), null, CurrentToken.Location));
                    }

                case TokenType.KwImplicit:
                case TokenType.KwExplicit:
                    {
                        return ParseOperatorOverride(new AstUnknownDecl(null, null, PeekToken().Location));
                    }

                case TokenType.Identifier:
                    {
                        var id = ParseIdentifierExpression();

                        // if it is a pointer or array type
                        while (CheckToken(TokenType.Asterisk) || CheckToken(TokenType.ArrayDef))
                        {
                            if (CheckToken(TokenType.ArrayDef))
                            {
                                // it is not allowed usually from ParseArrayExpr
                                if (!inInfo.AllowArrayExpression)
                                    break;
                                var arrExpr = new AstArrayExpr(id, new Location(id.RightPart.Beginning, CurrentToken.Location.Ending));
                                id = new AstNestedExpr(arrExpr, null, arrExpr);
                            }
                            else
                            {
                                // the check is done because of some misunderstood shite
                                // how to find out when 'a * b' is a mul expr
                                // and 'bool* bptr' is a ptr expr?
                                // so allowPointerExpressions is true only when decls are parsed!!!
                                if (inInfo.AllowMultiplyExpression || !IsThatPointerWithLookAhead(id))
                                    break;
                                var ptrExpr = new AstPointerExpr(id, false, new Location(id.RightPart.Beginning, CurrentToken.Location.Ending));
                                id = new AstNestedExpr(ptrExpr, null, ptrExpr);
                            }
                            NextToken();
                        }

                        // the second identifier for UnknownDecl
                        if (CheckToken(TokenType.Identifier))
                        {
                            var name = ParseIdentifierExpression(allowDots: false);
                            if (name.RightPart is not AstIdExpr idExpr)
                            {
                                ReportMessage(id.Location, [], ErrorCode.Get(CTEN.DeclNameIsNotIdent));
                                return id;
                            }
                            return new AstUnknownDecl(id, idExpr, new Location(token.Location, name.Location.Ending));
                        }

                        return id;
                    }

                case TokenType.Tilda:
                    {
                        NextToken();

                        var expr = ParseExpression(inInfo, ref outInfo);
                        if (expr is AstIdExpr idExpr)
                        {
                            idExpr.Suffix = "~";
                        }
                        else
                        {
                            // TODO: not only idents are allowed: ~(3 + 4)
                            ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.TildaUnexpectedExpr));
                        }
                        return expr;
                    }

                case TokenType.StringLiteral:
                    NextToken();
                    return new AstStringExpr((string)token.Data, token.Suffix, new Location(token.Location));

                case TokenType.CharLiteral:
                    NextToken();
                    return new AstCharExpr((string)token.Data, new Location(token.Location));

                case TokenType.NumberLiteral:
                    NextToken();
                    return new AstNumberExpr((NumberData)token.Data, token.Suffix, null, new Location(token.Location));

                case TokenType.KwTrue:
                    NextToken();
                    return new AstBoolExpr(true, new Location(token.Location));

                case TokenType.KwFalse:
                    NextToken();
                    return new AstBoolExpr(false, new Location(token.Location));

                case TokenType.OpenParen:
                    return ParseTupleExpression(inInfo, ref outInfo);

                default:
                    if (inInfo.Message != null && inInfo.Message.MessageArgs == null)
                        inInfo.Message.MessageArgs = [token.ToString()];
                    else if (inInfo.Message == null)
                        inInfo.Message = new MessageResolver() { MessageArgs = [token.Type.ToString(), token.Data.ToString()], XmlMessage = ErrorCode.Get(CTEN.CommonFailToParse) };
                    ReportMessage(token.Location, inInfo.Message.MessageArgs, inInfo.Message.XmlMessage);
                    NextToken(); // skip the token :)
                    return ParseEmptyExpression();
            }
        }
    }
}
