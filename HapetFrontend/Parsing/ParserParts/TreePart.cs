using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Runtime;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var savedMessage = inInfo.Message;
            inInfo.Message ??= new MessageResolver() { XmlMessage = ErrorCode.Get(CTEN.CommonUnexpectedInExpr) };

            var expr = ParseNullCoalescingExpression(inInfo, ref outInfo);

            inInfo.Message = savedMessage;

            return expr;
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseNullCoalescingExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var expr = ParseTernaryExpression(inInfo, ref outInfo);
            SkipNewlines(inInfo);
            // check for 'anime ?? cringe;'
            while (CheckToken(inInfo, TokenType.DoubleQuestion))
            {
                NextToken(inInfo);
                SkipNewlines(inInfo);

                // getting the right part
                var exprSecond = ParseTernaryExpression(inInfo, ref outInfo);

                // creating null comparison
                var nulll = new AstNullExpr(null, expr)
                { 
                    IsSyntheticStatement = true,
                };
                var nullComparison = new AstBinaryExpr("==", expr as AstExpression, nulll, expr)
                {
                    IsSyntheticStatement = true,
                };
                var ternOp = new AstTernaryExpr(nullComparison, exprSecond as AstExpression, 
                    expr as AstExpression, new Location(expr.Beginning, exprSecond.Ending));
                expr = ternOp;
                SkipNewlines(inInfo);
            }
            return expr;
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseTernaryExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var expr = ParseOrExpression(inInfo, ref outInfo);
            SkipNewlines(inInfo);

            if (CheckToken(inInfo, TokenType.QuestionMark))
            {
                NextToken(inInfo);
                SkipNewlines(inInfo);
                // ternary shite probably
                var trueExpr = ParseExpression(inInfo, ref outInfo);
                SkipNewlines(inInfo);
                Consume(inInfo, TokenType.Colon, ErrMsg(":", "in ternary expression"));
                SkipNewlines(inInfo);
                var falseExpr = ParseExpression(inInfo, ref outInfo);
                return new AstTernaryExpr(expr as AstExpression, trueExpr as AstExpression, falseExpr as AstExpression,
                    new Location(expr.Beginning, falseExpr.Ending));
            }
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

            while (CheckToken(inInfo, TokenType.KwIs))
            {
                var isTkn = NextToken(inInfo);
                SkipNewlines(inInfo);

                // handle 'is not' cringe
                bool isNot = false;
                if (PeekToken(inInfo).Data is string str && str == "not")
                {
                    NextToken(inInfo);
                    isNot = true;
                }

                // we want to prefer generics
                var saved1 = inInfo.PreferGenericShite;
                inInfo.PreferGenericShite = true;
                rhs = ParseAsExpression(inInfo, ref outInfo);
                inInfo.PreferGenericShite = saved1;

                // check for 'test is Anime anime' shite
                AstExpression additional = null;
                if (rhs is AstUnknownDecl udecl)
                {
                    additional = udecl.Name;
                    rhs = udecl.Type;
                }
                var binExpr = new AstBinaryExpr("is", lhs as AstExpression, rhs as AstExpression, new Location(lhs.Beginning, rhs.Ending))
                {
                    IsNot = isNot,
                    OperatorTokenLocation = isTkn?.Location,
                };

                // handling additional shite
                if (additional is AstIdExpr idExpr)
                {
                    AstBinaryExpr asExpr = binExpr.GetDeepCopy() as AstBinaryExpr;
                    asExpr.Operator = "as";
                    asExpr.IsFromIsOperator = true;
                    asExpr.IsSyntheticStatement = true;

                    // creating deep copies of its elements
                    // because we don't want to change 
                    // original shite' scopes and other
                    AstVarDecl varDecl = new AstVarDecl(
                        binExpr.Right.GetDeepCopy() as AstExpression,
                        idExpr.GetDeepCopy() as AstIdExpr,
                        asExpr.GetDeepCopy() as AstExpression,
                        "", binExpr)
                    {
                        IsSyntheticStatement = true,
                    };
                    // add when not look ahead
                    if (!inInfo.IsLookAheadParsing)
                        outInfo.StatementsToAddBefore.Add(varDecl);
                }

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

            while (CheckToken(inInfo, TokenType.KwAs))
            {
                var asTkn = NextToken(inInfo);
                SkipNewlines(inInfo);

                // we want to prefer generics
                var saved1 = inInfo.PreferGenericShite;
                inInfo.PreferGenericShite = true;
                rhs = ParseInExpression(inInfo, ref outInfo);
                inInfo.PreferGenericShite = saved1;

                var binExpr = new AstBinaryExpr("as", lhs as AstExpression, rhs as AstExpression, new Location(lhs.Beginning, rhs.Ending))
                {
                    OperatorTokenLocation = asTkn?.Location,
                };

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

            while (CheckToken(inInfo, TokenType.KwIn))
            {
                var inTkn = NextToken(inInfo);
                SkipNewlines(inInfo);
                rhs = ParseComparisonExpression(inInfo, ref outInfo);
                var binExpr = new AstBinaryExpr("in", lhs as AstExpression, rhs as AstExpression, new Location(lhs.Beginning, rhs.Ending))
                {
                    OperatorTokenLocation = inTkn?.Location,
                };

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
                SkipNewlines(inInfo);
                var next = PeekToken(inInfo);

                // cringe handle >>
                // https://github.com/dotnet/roslyn/blob/62646c22f6bd7b213e7e15dbc0dfadfe47a1e30f/src/Compilers/CSharp/Portable/Parser/Lexer.cs#L4118-L4122
                // https://github.com/dotnet/roslyn/blob/62646c22f6bd7b213e7e15dbc0dfadfe47a1e30f/src/Compilers/CSharp/Portable/Parser/LanguageParser.cs#L11067-L11073
                var savedLookAhead = inInfo.IsLookAheadParsing;
                inInfo.IsLookAheadParsing = true;
                // should be done only once
                if (!savedLookAhead)
                    UpdateLookAheadLocation();
                SaveLookAheadLocation();
                var t1 = PeekToken(inInfo);
                NextToken(inInfo);
                var t2 = PeekToken(inInfo);
                if (t1.Type == TokenType.Greater && t2.Type == TokenType.Greater)
                {
                    next.Type = TokenType.GreaterGreater;
                    next.Location.End = t2.Location.End;
                }
                RestoreLookAheadLocation();
                inInfo.IsLookAheadParsing = savedLookAhead;

                var op = tokenMapping(next.Type);
                if (op == null)
                {
                    return lhs;
                }

                // make one more token eat because of pseudo >>
                if (next.Type == TokenType.GreaterGreater)
                    NextToken(inInfo);

                NextToken(inInfo);
                SkipNewlines(inInfo);
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
            AstExpression toReturn = null;
            var next = PeekToken(inInfo);
            if (next.Type == TokenType.Ampersand)
            {
                NextToken(inInfo);
                SkipNewlines(inInfo);

                var sub = ParseUnaryExpression(inInfo, ref outInfo);
                if (sub is not AstExpression expr)
                {
                    ReportMessage(sub, ["&"], ErrorCode.Get(CTEN.ExprExpectedInUnExpr));
                    return sub;
                }
                toReturn = new AstAddressOfExpr(expr, new Location(next.Location, sub.Ending));
            }
            else if (next.Type == TokenType.Asterisk)
            {
                NextToken(inInfo);
                SkipNewlines(inInfo);
                var sub = ParseUnaryExpression(inInfo, ref outInfo);
                if (sub is not AstExpression expr)
                {
                    ReportMessage(sub, ["*"], ErrorCode.Get(CTEN.ExprExpectedInUnExpr));
                    return sub;
                }
                toReturn = new AstPointerExpr(expr, true, new Location(next.Location, sub.Ending));
            }
            else if (next.Type == TokenType.Minus || next.Type == TokenType.Plus)
            {
                NextToken(inInfo);
                SkipNewlines(inInfo);
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
                toReturn = un;
            }
            else if (next.Type == TokenType.Bang)
            {
                NextToken(inInfo);
                SkipNewlines(inInfo);
                var sub = ParseUnaryExpression(inInfo, ref outInfo);
                var un = new AstUnaryExpr("!", sub as AstExpression, new Location(next.Location, sub.Ending));
                // error if it is not an expr
                if (sub is not AstExpression)
                {
                    ReportMessage(sub, [un.Operator], ErrorCode.Get(CTEN.ExprExpectedInUnExpr));
                }
                toReturn = un;
            }
            else if (next.Type == TokenType.PlusPlus || next.Type == TokenType.MinusMinus)
            {
                NextToken(inInfo);
                SkipNewlines(inInfo);
                var sub = ParseUnaryExpression(inInfo, ref outInfo);
                var op = next.Type == TokenType.PlusPlus ? "++" : "--";
                var un = new AstUnaryIncDecExpr(op, sub as AstExpression, new Location(next.Location, sub.Ending)) { IsPrefix = true };
                // error if it is not a nested!!!
                if (sub is not AstNestedExpr)
                {
                    ReportMessage(sub, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                }
                toReturn = un;
            }
            else if (next.Type == TokenType.Tilda)
            {
                NextToken(inInfo);
                SkipNewlines(inInfo);
                var sub = ParseUnaryExpression(inInfo, ref outInfo);
                var un = new AstUnaryExpr("~", sub as AstExpression, new Location(next.Location, sub.Ending));
                // error if it is not an expr
                if (sub is not AstExpression)
                {
                    ReportMessage(sub, [], ErrorCode.Get(CTEN.TildaUnexpectedExpr));
                }
                toReturn = un;
            }

            // it should be wrapped into nested
            if (toReturn != null)
                return new AstNestedExpr(toReturn, null, toReturn.Location);

            return ParsePostUnaryExpression(inInfo, ref outInfo);
        }

        private AstStatement ParsePostUnaryExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var expr = ParseAtomicExpression(inInfo, ref outInfo);
            SkipNewlines(inInfo);

            // just move forward the udecl :)
            if (expr is AstUnknownDecl)
                return expr;

            while (true)
            {
                switch (PeekToken(inInfo).Type)
                {
                    case TokenType.OpenParen:
                        {
                            var savedPrev = inInfo.PreviousNestedForNullCheck;
                            inInfo.PreviousNestedForNullCheck = null;
                            // TODO: not only nested should be allowed. tuples, lamdas and other shite
                            var args = ParseArgumentList(inInfo, ref outInfo, out var _, out var end);
                            inInfo.PreviousNestedForNullCheck = savedPrev;

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

                            SkipNewlines(inInfo);
                            bool dotsAfter = CheckToken(inInfo, TokenType.Period);

                            var callExpr = new AstCallExpr(nestExpr.LeftPart, idExpr.GetCopy(), args, new Location(expr.Beginning, end));
                            expr = new AstNestedExpr(callExpr, null, callExpr);

                            // check for dots after this!!! there could be a.asd().asd().ddd().d.lll()
                            // for better understand imagine we have 'anime.Asd().dwd.Lmao();'
                            // and this is the first entry. So we already parsed here 'anime.Asd()' 
                            // and need to check the rest shite
                            if (dotsAfter)
                            {
                                NextToken(inInfo);
                                SkipNewlines(inInfo);
                                // here we are getting the rest 'dwd.Lmao'
                                expr = ParseIdentifierExpression(inInfo, iniNested: expr as AstNestedExpr);
                                // so after this the upper loop will check if there is a OpenParent and so on
                                // if there is no OpenParen - then just NestedExpr will be returned
                            }
                        }
                        break;

                    case TokenType.OpenBracket:
                        {
                            NextToken(inInfo);
                            SkipNewlines(inInfo);

                            var args = new List<AstStatement>();
                            while (true)
                            {
                                var next = PeekToken(inInfo);
                                if (next.Type == TokenType.CloseBracket || next.Type == TokenType.EOF)
                                    break;

                                args.Add(ParseExpression(inInfo, ref outInfo));
                                SkipNewlines(inInfo);

                                next = PeekToken(inInfo);
                                if (next.Type == TokenType.Comma)
                                {
                                    NextToken(inInfo);
                                    SkipNewlines(inInfo);
                                }
                                else if (next.Type == TokenType.CloseBracket)
                                    break;
                                else
                                {
                                    NextToken(inInfo);
                                    ReportMessage(next.Location, [], ErrorCode.Get(CTEN.ArrayAccUnexpectedToken));
                                }
                            }
                            var end = Consume(inInfo, TokenType.CloseBracket, ErrMsg("]", "at end of [..] operator")).Location;
                            if (args.Count == 0)
                            {
                                ReportMessage(end, [], ErrorCode.Get(CTEN.ArrayAccNoArgs));
                                args.Add(ParseEmptyExpression(inInfo));
                            }
                            else if (args.Count > 1)
                            {
                                ReportMessage(end, [], ErrorCode.Get(CTEN.ArrayAccTooManyArgs));
                            }

                            var arrAcc = new AstArrayAccessExpr(expr as AstExpression, args[0] as AstExpression, new Location(expr.Beginning, end));
                            expr = new AstNestedExpr(arrAcc, null, arrAcc);

                            SkipNewlines(inInfo);
                            // check for dots after this!!! there could be a.arr[i].Length
                            // for better understand imagine we have 'a.arr[i].Length;'
                            // and this is the first entry. So we already parsed here 'a.arr[i]' 
                            // and need to check the rest shite
                            if (CheckToken(inInfo, TokenType.Period))
                            {
                                NextToken(inInfo);
                                SkipNewlines(inInfo);
                                // here we are getting the rest '.Length'
                                expr = ParseIdentifierExpression(inInfo, iniNested: expr as AstNestedExpr);
                                // so after this the upper loop will check if there is a OpenParent and so on
                                // if there is no OpenParen - then just NestedExpr will be returned
                            }
                        }
                        break;

                    case TokenType.PlusPlus:
                    case TokenType.MinusMinus:
                        {
                            var tkn = NextToken(inInfo);
                            SkipNewlines(inInfo);
                            var op = tkn.Type == TokenType.PlusPlus ? "++" : "--";
                            var un = new AstUnaryIncDecExpr(op, expr as AstExpression, new Location(expr.Beginning, tkn.Location)) { IsPrefix = false };
                            // error if it is not a nested!!!
                            if (expr is not AstNestedExpr)
                            {
                                ReportMessage(expr, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                            }
                            return un;
                        }

                    case TokenType.Period:
                        {
                            // cringe when smth like
                            // ... (pivo as Sperm).CoolCringe().WWW ...
                            var tkn = NextToken(inInfo);
                            SkipNewlines(inInfo);
                            var tmpExpr = new AstNestedExpr(expr as AstExpression, null, expr);
                            expr = ParseIdentifierExpression(inInfo, iniNested: tmpExpr);
                        }
                        break;

                    case TokenType.QuestionMark:
                        {
                            // need to check further with look ahead
                            var savedLookAhead = inInfo.IsLookAheadParsing;
                            inInfo.IsLookAheadParsing = true;
                            // should be done only once
                            if (!savedLookAhead)
                                UpdateLookAheadLocation();
                            SaveLookAheadLocation();
                            NextToken(inInfo);
                            bool reseted = false;
                            if (CheckToken(inInfo, TokenType.Period))
                            {
                                reseted = true;
                                RestoreLookAheadLocation();
                                inInfo.IsLookAheadParsing = savedLookAhead;

                                // null check access: 'var a = Anime?.Pivo;'
                                NextToken(inInfo); // skip ?
                                NextToken(inInfo); // skip .

                                var savedPrev = inInfo.PreviousNestedForNullCheck;

                                // making normal nested
                                var iniNest = expr is AstNestedExpr ? expr : new AstNestedExpr(expr as AstExpression, savedPrev, expr);
                                inInfo.PreviousNestedForNullCheck = iniNest.GetDeepCopy() as AstNestedExpr;

                                // creating null comparison
                                var nulll = new AstNullExpr(null, expr);
                                var nullComparison = new AstBinaryExpr("==", inInfo.PreviousNestedForNullCheck, nulll, expr);
                                var normalPart = ParsePostUnaryExpression(inInfo, ref outInfo) as AstExpression;
                                var ternOp = new AstTernaryExpr(nullComparison, nulll, normalPart, expr);

                                inInfo.PreviousNestedForNullCheck = savedPrev;
                                expr = ternOp;
                            }
                            if (!reseted)
                                RestoreLookAheadLocation();
                            inInfo.IsLookAheadParsing = savedLookAhead;

                            // or just return expr if non .?
                            return expr;
                        }

                    default:
                        return expr;
                }
            }
        }

        private AstStatement ParseAtomicExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var token = PeekToken(inInfo);
            switch (token.Type)
            {
                case TokenType.KwDefault:
                    {
                        NextToken(inInfo);

                        // it is a default case
                        SkipNewlines(inInfo);
                        if (inInfo.ExpectDefaultCase)
                        {
                            return ParseCaseStatement(inInfo, ref outInfo, true, token.Location);
                        }

                        // only type is expected
                        AstExpression typeExpr = null;
                        if (CheckToken(inInfo, TokenType.OpenParen))
                        {
                            NextToken(inInfo);
                            var saved = inInfo.AllowMultiplyExpression;
                            inInfo.AllowMultiplyExpression = false;
                            typeExpr = ParseAtomicExpression(inInfo, ref outInfo) as AstExpression;
                            inInfo.AllowMultiplyExpression = saved;
                            Consume(inInfo, TokenType.CloseParen, ErrMsg(")", "after default' type arg"));
                        }

                        // it is just a 'default' word
                        return new AstDefaultExpr(new Location(token.Location)) { TypeForDefault = typeExpr };
                    }

                case TokenType.KwChecked:
                case TokenType.KwUnchecked:
                    {
                        NextToken(inInfo);

                        if (CheckToken(inInfo, TokenType.OpenParen))
                        {
                            NextToken(inInfo);
                            var saved = inInfo.AllowMultiplyExpression;
                            inInfo.AllowMultiplyExpression = true;
                            var subExpr = ParseExpression(inInfo, ref outInfo) as AstExpression;
                            inInfo.AllowMultiplyExpression = saved;
                            Consume(inInfo, TokenType.CloseParen, ErrMsg(")", "after checked/unchecked expression"));

                            return new AstCheckedExpr(subExpr, new Location(token.Location)) { IsChecked = (token.Type == TokenType.KwChecked) };
                        }
                        else
                        {
                            // TODO: also allow blocked checked https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/checked-and-unchecked
                            // TODO: make it as AstCheckedStmt
                            throw new NotImplementedException();
                        }
                    }

                case TokenType.KwTypeof:
                case TokenType.KwSizeof:
                case TokenType.KwAlignof:
                case TokenType.KwNameof:
                    {
                        NextToken(inInfo);

                        Consume(inInfo, TokenType.OpenParen, ErrMsg("(", "after sizeof/alignof/typeof expression"));

                        var saved = inInfo.AllowMultiplyExpression;
                        var saved1 = inInfo.PreferGenericShite;
                        inInfo.AllowMultiplyExpression = false;
                        inInfo.PreferGenericShite = true;
                        var subExpr = ParseExpression(inInfo, ref outInfo) as AstExpression;
                        inInfo.AllowMultiplyExpression = saved;
                        inInfo.PreferGenericShite = saved1;

                        Consume(inInfo, TokenType.CloseParen, ErrMsg(")", "after sizeof/alignof/typeof expression"));

                        var nst = subExpr as AstNestedExpr;
                        Debug.Assert(nst != null);
                        return new AstSATOfExpr(nst, token.Type, new Location(token.Location));
                    }

                case TokenType.KwNull:
                    NextToken(inInfo);
                    return new AstNullExpr(null, new Location(token.Location));

                case TokenType.KwNew:
                case TokenType.KwStackAlloc:
                    {
                        return ParseNewExpression(inInfo, ref outInfo, token.Type == TokenType.KwStackAlloc);
                    }

                case TokenType.KwBase:
                    {
                        // this is when calling base func from child class like:
                        // public override void Anime()
                        // {
                        //     base.Anime();
                        // }
                        NextToken(inInfo);
                        Consume(inInfo, TokenType.Period, ErrMsg(".", "after 'base' word"));
                        return ParseIdentifierExpression(inInfo, iniNested: new AstNestedExpr(new AstIdExpr("base", CurrentToken.Location), null, CurrentToken.Location));
                    }

                case TokenType.KwEvent:
                    {
                        NextToken(inInfo);
                        var udecl = ParseAtomicExpression(inInfo, ref outInfo);
                        if (udecl is AstUnknownDecl u)
                            u.IsEvent = true;
                        return udecl;
                    }

                case TokenType.KwImplicit:
                case TokenType.KwExplicit:
                    {
                        return ParseOperatorOverride(inInfo, ref outInfo, new AstUnknownDecl(null, null, PeekToken(inInfo).Location));
                    }

                case TokenType.Identifier:
                    {
                        // should be done only once
                        if (!inInfo.IsLookAheadParsing)
                            UpdateLookAheadLocation();
                        SaveLookAheadLocation();
                        // need to check for => after the id - if it there - it is a lambda
                        var saved = inInfo.IsLookAheadParsing;
                        inInfo.IsLookAheadParsing = true;
                        var _ = ParseIdentifierExpression(inInfo, allowDots: false, allowGenerics: false, expectIdent: true);
                        if (CheckToken(inInfo, TokenType.Arrow))
                        {
                            inInfo.IsLookAheadParsing = saved;
                            RestoreLookAheadLocation();
                            return ParseLambdaDecl(inInfo, ref outInfo);
                        }
                        inInfo.IsLookAheadParsing = saved;
                        RestoreLookAheadLocation();

                        var id = ParseIdentifierExpression(inInfo, iniNested: inInfo.PreviousNestedForNullCheck);

                        // if there is a ? need to check that it is not ternary or other shite
                        if (CheckToken(inInfo, TokenType.QuestionMark))
                        {
                            var saved2 = inInfo.IsLookAheadParsing;
                            inInfo.IsLookAheadParsing = true;
                            // should be done only once
                            if (!saved2)
                                UpdateLookAheadLocation();
                            SaveLookAheadLocation();
                            NextToken(inInfo);

                            bool isNullable;
                            var next = PeekToken(inInfo);
                            // if '(SomeType?)' casting
                            if (next.Type == TokenType.CloseParen)
                                isNullable = true;
                            // skip 'SomeShite?.'
                            else if (next.Type == TokenType.Period)
                                isNullable = false;
                            // check if no : after
                            else
                            {
                                var __ = ParseExpression(inInfo, ref outInfo);
                                isNullable = !CheckToken(inInfo, TokenType.Colon);
                            }
                            inInfo.IsLookAheadParsing = saved2;
                            RestoreLookAheadLocation();

                            // if it is nullable
                            if (isNullable)
                            {
                                var q = NextToken(inInfo);
                                id = new AstNestedExpr(new AstNullableExpr(id, q.Location), null, id.Location);
                            }
                        }

                        // if it is a pointer or array type
                        while (CheckToken(inInfo, TokenType.Asterisk) || CheckToken(inInfo, TokenType.ArrayDef))
                        {
                            if (CheckToken(inInfo, TokenType.ArrayDef))
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
                                if (!IsThatPointerWithLookAhead(inInfo, inInfo.AllowMultiplyExpression))
                                    break;
                                var ptrExpr = new AstPointerExpr(id, false, new Location(id.RightPart.Beginning, CurrentToken.Location.Ending));
                                id = new AstNestedExpr(ptrExpr, null, ptrExpr);
                            }
                            NextToken(inInfo);
                        }

                        // the second identifier for UnknownDecl
                        if (CheckToken(inInfo, TokenType.Identifier))
                        {
                            // allowDots is true because of explicit interface impls
                            // allowGenerics because of Anime<T>.Func explicit impls
                            var name = ParseIdentifierExpression(inInfo, allowDots: true, allowGenerics: true, expectIdent: true, allowTupled: (inInfo.AllowTypedTuple && !inInfo.IsInTupleParsing));
                            if (name.RightPart is not AstIdExpr idExpr)
                            {
                                ReportMessage(id.Location, [], ErrorCode.Get(CTEN.DeclNameIsNotIdent));
                                return id;
                            }
                            return new AstUnknownDecl(id, idExpr, new Location(token.Location, name.Location.Ending));
                        }

                        return id;
                    }

                case TokenType.StringLiteral:
                    NextToken(inInfo);
                    return new AstStringExpr((string)token.Data, token.Suffix, new Location(token.Location));

                case TokenType.CharLiteral:
                    NextToken(inInfo);
                    return new AstCharExpr((string)token.Data, new Location(token.Location));

                case TokenType.NumberLiteral:
                    NextToken(inInfo);
                    return new AstNumberExpr((NumberData)token.Data, token.Suffix, null, new Location(token.Location));

                case TokenType.KwTrue:
                    NextToken(inInfo);
                    return new AstBoolExpr(true, new Location(token.Location));

                case TokenType.KwFalse:
                    NextToken(inInfo);
                    return new AstBoolExpr(false, new Location(token.Location));

                case TokenType.OpenParen:
                    return ParseTupleExpression(inInfo, ref outInfo);

                default:
                    if (inInfo.Message != null && inInfo.Message.MessageArgs == null)
                        inInfo.Message.MessageArgs = [token.ToString()];
                    else if (inInfo.Message == null)
                        inInfo.Message = new MessageResolver() { MessageArgs = [token.Type.ToString(), token.Data.ToString()], XmlMessage = ErrorCode.Get(CTEN.CommonFailToParse) };
                    ReportMessage(token.Location, inInfo.Message.MessageArgs, inInfo.Message.XmlMessage);
                    NextToken(inInfo); // skip the token :)
                    return ParseEmptyExpression(inInfo);
            }
        }
    }
}
