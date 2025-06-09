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
            // check for 'anime ?? cringe;'
            if (CheckToken(TokenType.DoubleQuestion))
            {
                NextToken();

                // getting the right part
                var exprSecond = ParseTernaryExpression(inInfo, ref outInfo);

                // creating null comparison
                var nulll = new AstNullExpr(null, expr);
                var nullComparison = new AstBinaryExpr("==", expr as AstExpression, nulll, expr);
                var ternOp = new AstTernaryExpr(nullComparison, exprSecond as AstExpression, 
                    expr as AstExpression, new Location(expr.Beginning, exprSecond.Ending));
                expr = ternOp;
            }
            return expr;
        }

        [DebuggerStepThrough]
        [StackTraceHidden]
        [DebuggerHidden]
        private AstStatement ParseTernaryExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var expr = ParseOrExpression(inInfo, ref outInfo);

            if (CheckToken(TokenType.QuestionMark))
            {
                NextToken();

                // ternary shite probably
                var trueExpr = ParseExpression(inInfo, ref outInfo);
                Consume(TokenType.Colon, ErrMsg(":", "in ternary expression"));
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

            while (CheckToken(TokenType.KwIs))
            {
                var _ = NextToken();
                SkipNewlines();

                // handle 'is not' cringe
                bool isNot = false;
                if (PeekToken().Data is string str && str == "not")
                {
                    NextToken();
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
                };

                // handling additional shite
                if (additional is AstIdExpr idExpr)
                {
                    AstBinaryExpr asExpr = binExpr.GetDeepCopy() as AstBinaryExpr;
                    asExpr.Operator = "as";
                    asExpr.IsFromIsOperator = true;

                    // creating deep copies of its elements
                    // because we don't want to change 
                    // original shite' scopes and other
                    AstVarDecl varDecl = new AstVarDecl(
                        binExpr.Right.GetDeepCopy() as AstExpression,
                        idExpr.GetDeepCopy() as AstIdExpr,
                        asExpr.GetDeepCopy() as AstExpression,
                        "", binExpr);
                    outInfo.IsOpDeclarations.Add(varDecl);
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

            while (CheckToken(TokenType.KwAs))
            {
                var _ = NextToken();
                SkipNewlines();

                // we want to prefer generics
                var saved1 = inInfo.PreferGenericShite;
                inInfo.PreferGenericShite = true;
                rhs = ParseInExpression(inInfo, ref outInfo);
                inInfo.PreferGenericShite = saved1;

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
                SkipNewlines();
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

            while (true)
            {
                switch (PeekToken().Type)
                {
                    case TokenType.OpenParen:
                        {
                            // TODO: not only nested should be allowed. tuples, lamdas and other shite
                            var args = ParseArgumentList(inInfo, ref outInfo, out var _, out var end);
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
                                expr = ParseIdentifierExpression(inInfo, iniNested: expr as AstNestedExpr);
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
                                expr = ParseIdentifierExpression(inInfo, iniNested: expr as AstNestedExpr);
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

                    case TokenType.Period:
                        {
                            // cringe when smth like
                            // ... (pivo as Sperm).CoolCringe().WWW ...
                            var tkn = NextToken();
                            SkipNewlines();
                            var tmpExpr = new AstNestedExpr(expr as AstExpression, null, expr);
                            expr = ParseIdentifierExpression(inInfo, iniNested: tmpExpr);
                        }
                        break;

                    case TokenType.QuestionMark:
                        {
                            // need to check further with look ahead
                            UpdateLookAheadLocation();
                            NextLookAhead();
                            if (CheckLookAhead(TokenType.Period))
                            {
                                // null check access: 'var a = Anime?.Pivo;'
                                NextToken(); // skip ?
                                NextToken(); // skip .

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

                case TokenType.KwChecked:
                case TokenType.KwUnchecked:
                    {
                        NextToken();

                        if (CheckToken(TokenType.OpenParen))
                        {
                            NextToken();
                            var saved = inInfo.AllowMultiplyExpression;
                            inInfo.AllowMultiplyExpression = true;
                            var subExpr = ParseExpression(inInfo, ref outInfo) as AstExpression;
                            inInfo.AllowMultiplyExpression = saved;
                            Consume(TokenType.CloseParen, ErrMsg(")", "after checked/unchecked' sub expression"));

                            return new AstCheckedExpr(subExpr, new Location(token.Location)) { IsChecked = (token.Type == TokenType.KwChecked) };
                        }
                        else
                        {
                            // TODO: also allow blocked checked https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/checked-and-unchecked
                            // TODO: make it as AstCheckedStmt
                            throw new NotImplementedException();
                        }
                    }

                case TokenType.KwNull:
                    NextToken();
                    return new AstNullExpr(null, new Location(token.Location));

                case TokenType.KwNew:
                    {
                        return ParseNewExpression(inInfo, ref outInfo);
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
                        return ParseIdentifierExpression(inInfo, iniNested: new AstNestedExpr(new AstIdExpr("base", CurrentToken.Location), null, CurrentToken.Location));
                    }

                case TokenType.KwImplicit:
                case TokenType.KwExplicit:
                    {
                        return ParseOperatorOverride(inInfo, ref outInfo, new AstUnknownDecl(null, null, PeekToken().Location));
                    }

                case TokenType.Identifier:
                    {
                        var id = ParseIdentifierExpression(inInfo, iniNested: inInfo.PreviousNestedForNullCheck);

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
                                if (!IsThatPointerWithLookAhead(inInfo, inInfo.AllowMultiplyExpression))
                                    break;
                                var ptrExpr = new AstPointerExpr(id, false, new Location(id.RightPart.Beginning, CurrentToken.Location.Ending));
                                id = new AstNestedExpr(ptrExpr, null, ptrExpr);
                            }
                            NextToken();
                        }

                        // the second identifier for UnknownDecl
                        if (CheckToken(TokenType.Identifier))
                        {
                            // allowDots is true because of explicit interface impls
                            // allowGenerics because of Anime<T>.Func explicit impls
                            var name = ParseIdentifierExpression(inInfo, allowDots: true, allowGenerics: true, expectIdent: true, allowTupled: inInfo.AllowTypedTuple);
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
