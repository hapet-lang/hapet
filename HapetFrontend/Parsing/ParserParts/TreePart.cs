using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using System.Diagnostics;

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
        private AstStatement ParseOrExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseAndExpression, inInfo, ref outInfo,
                (TokenType.LogicalOr, "||"));
        }

        [DebuggerStepThrough]
        private AstStatement ParseAndExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseIsExpression, inInfo, ref outInfo,
                (TokenType.LogicalAnd, "&&"));
        }

        private AstStatement ParseIsExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var lhs = ParseAsExpression(inInfo, ref outInfo);
            AstStatement rhs = null;

            while (CheckToken(TokenType.KwIs))
            {
                var _is = NextToken();
                SkipNewlines();
                rhs = ParseAsExpression(inInfo, ref outInfo);
                lhs = new AstBinaryExpr("is", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
            }
            return lhs;
        }

        private AstStatement ParseAsExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var lhs = ParseInExpression(inInfo, ref outInfo);
            AstStatement rhs = null;

            while (CheckToken(TokenType.KwAs))
            {
                var _as = NextToken();
                SkipNewlines();
                rhs = ParseInExpression(inInfo, ref outInfo);
                lhs = new AstBinaryExpr("as", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
            }
            return lhs;
        }

        private AstStatement ParseInExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var lhs = ParseComparisonExpression(inInfo, ref outInfo);
            AstStatement rhs = null;

            while (CheckToken(TokenType.KwIn))
            {
                var _in = NextToken();
                SkipNewlines();
                rhs = ParseComparisonExpression(inInfo, ref outInfo);
                lhs = new AstBinaryExpr("in", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
            }
            return lhs;
        }

        [DebuggerStepThrough]
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
        private AstStatement ParseBitAndOrExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseBitShiftExpression, inInfo, ref outInfo,
                (TokenType.Ampersand, "&"),
                (TokenType.VerticalSlash, "|"));
        }

        [DebuggerStepThrough]
        private AstStatement ParseBitShiftExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseAddSubExpression, inInfo, ref outInfo,
                (TokenType.LessLess, "<<"),
                (TokenType.GreaterGreater, ">>"));
        }

        [DebuggerStepThrough]
        private AstStatement ParseAddSubExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseMulDivExpression, inInfo, ref outInfo,
                (TokenType.Plus, "+"),
                (TokenType.Minus, "-"));
        }

        [DebuggerStepThrough]
        private AstStatement ParseMulDivExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseUnaryExpression, inInfo, ref outInfo,
                (TokenType.Asterisk, "*"),
                (TokenType.ForwardSlash, "/"),
                (TokenType.Percent, "%"));
        }

        [Obsolete("Use ParseMulDivExpression")]
        [DebuggerStepThrough]
        private AstStatement ParseBinaryExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            return ParseBinaryLeftAssociativeExpression(ParseUnaryExpression, inInfo, ref outInfo,
                (TokenType.Asterisk, "*"),
                (TokenType.ForwardSlash, "/"),
                (TokenType.Percent, "%"));
        }

        [DebuggerStepThrough]
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

                var op = tokenMapping(next.Type);
                if (op == null)
                {
                    return lhs;
                }

                NextToken();
                SkipNewlines();
                rhs = sub(inInfo, ref outInfo);
                var binExpr = new AstBinaryExpr(op, lhs, rhs, new Location(lhs.Beginning, rhs.Ending));

                // error if it is not an expr
                if (binExpr.Left is not AstExpression)
                {
                    ReportMessage(binExpr.Left, [binExpr.Operator], ErrorCode.Get(CTEN.ExprsExpectedInBinExpr));
                }
                // error if it is not an expr
                if (binExpr.Right is not AstExpression)
                {
                    ReportMessage(binExpr.Right, [binExpr.Operator], ErrorCode.Get(CTEN.ExprsExpectedInBinExprR));
                }

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
                    ReportMessage(sub.Location, ["&"], ErrorCode.Get(CTEN.ExprExpectedInUnExpr));
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
                    ReportMessage(sub.Location, ["*"], ErrorCode.Get(CTEN.ExprExpectedInUnExpr));
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
                var un = new AstUnaryExpr(op, sub, new Location(next.Location, sub.Ending));
                // error if it is not an expr
                if (un.SubExpr is not AstExpression)
                {
                    ReportMessage(un.SubExpr, [un.Operator], ErrorCode.Get(CTEN.ExprExpectedInUnExpr));
                }
                return un;
            }
            else if (next.Type == TokenType.Bang)
            {
                NextToken();
                SkipNewlines();
                var sub = ParseUnaryExpression(inInfo, ref outInfo);
                var un = new AstUnaryExpr("!", sub, new Location(next.Location, sub.Ending));
                // error if it is not an expr
                if (un.SubExpr is not AstExpression)
                {
                    ReportMessage(un.SubExpr, [un.Operator], ErrorCode.Get(CTEN.ExprExpectedInUnExpr));
                }
                return un;
            }

            return ParsePostUnaryExpression(inInfo, ref outInfo);
        }

        private AstStatement ParsePostUnaryExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var savedAllowence = inInfo.AllowArrayExpression;
            inInfo.AllowArrayExpression = true;
            var expr = ParseAtomicExpression(inInfo, ref outInfo);
            inInfo.AllowArrayExpression = savedAllowence;

            bool breakLoop = false;
            while (!breakLoop)
            {
                switch (PeekToken().Type)
                {
                    case TokenType.OpenParen:
                        {
                            // if it is func decl, not call
                            if (inInfo.AllowFunctionDeclaration)
                            {
                                breakLoop = true;
                                break;
                            }

                            // TODO: not only nested should be allowed. tuples, lamdas and other shite
                            var args = ParseArgumentList(out var end);
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

                                bool savedAllowComma = inInfo.AllowCommaForTuple;
                                inInfo.AllowCommaForTuple = true;
                                args.Add(ParseExpression(inInfo, ref outInfo));
                                inInfo.AllowCommaForTuple = savedAllowComma;

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
                                    //RecoverExpression();
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

                            if (args.First() is not AstExpression firstExpr)
                            {
                                // if it is not an expr - then this is probably an indexer overload
                                if (args.First() is not UnknownDecl unknownDecl || 
                                    expr is not UnknownDecl indexerDecl || 
                                    indexerDecl.Name.Name != "this")
                                {
                                    ReportMessage(args.First().Location, [], ErrorCode.Get(CTEN.ArrayAccNotExpr));
                                    return expr;
                                }

                                SkipNewlines();

                                // TODO: doc 
                                indexerDecl.Name = indexerDecl.Name.GetCopy("indexer__");
                                var prop = PreparePropertyDecl(indexerDecl, "") as AstPropertyDecl;
                                var indexer = new AstIndexerDecl(prop);
                                indexer.IndexerParameter = new AstParamDecl(unknownDecl.Type, unknownDecl.Name, null, "", unknownDecl);

                                return indexer;
                            }
                            var arrAcc = new AstArrayAccessExpr(expr as AstExpression, firstExpr, new Location(expr.Beginning, end));
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
                case TokenType.KwBreak:
                case TokenType.KwContinue:
                    NextToken();
                    return new AstBreakContStmt(token.Type == TokenType.KwBreak, new Location(token.Location));

                case TokenType.KwDefault:
                    {
                        NextToken();
                        // it is just a 'default' word
                        return new AstDefaultExpr(new Location(token.Location));
                    }

                case TokenType.KwNull:
                    NextToken();
                    return new AstNullExpr(PointerType.NullLiteralType, new Location(token.Location));

                case TokenType.OpenBracket:
                    return ParseAttributeStatement();

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
                        return ParseOperatorOverride(new UnknownDecl(null, null, PeekToken().Location));
                    }

                case TokenType.Identifier:
                    {
                        var id = ParseIdentifierExpression(allowGenerics: inInfo.AllowGeneric);

                        // if it is a pointer or array type
                        while (CheckToken(TokenType.Asterisk) || CheckToken(TokenType.ArrayDef))
                        {
                            if (CheckToken(TokenType.ArrayDef))
                            {
                                // it is not allowed usually from ParseArrayExpr
                                if (!inInfo.AllowArrayExpression)
                                    break;
                                var arrExpr = new AstArrayExpr(id.RightPart, new Location(id.RightPart.Beginning, CurrentToken.Location.Ending));
                                id.RightPart = arrExpr;
                            }
                            else
                            {
                                // the check is done because of some misunderstood shite
                                // how to find out when 'a * b' is a mul expr
                                // and 'bool* bptr' is a ptr expr?
                                // so allowPointerExpressions is true only when decls are parsed!!!
                                if (!inInfo.AllowPointerExpression)
                                    break;
                                var ptrExpr = new AstPointerExpr(id.RightPart, false, new Location(id.RightPart.Beginning, CurrentToken.Location.Ending));
                                id.RightPart = ptrExpr;
                            }
                            NextToken();
                        }

                        // the second identifier for UnknownDecl
                        if (CheckToken(TokenType.Identifier))
                        {
                            var name = ParseIdentifierExpression(allowDots: false, allowGenerics: inInfo.AllowGeneric);
                            if (name.RightPart is not AstIdExpr idExpr)
                            {
                                ReportMessage(id.Location, [], ErrorCode.Get(CTEN.DeclNameIsNotIdent));
                                return id;
                            }
                            return new UnknownDecl(id, idExpr, new Location(token.Location, name.Location.Ending));
                        }

                        return id;
                    }

                case TokenType.Tilda:
                    {
                        NextToken();

                        var savedAllowPointer = inInfo.AllowPointerExpression;
                        inInfo.AllowPointerExpression = false;
                        var expr = ParseExpression(inInfo, ref outInfo);
                        inInfo.AllowPointerExpression = savedAllowPointer;

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

                case TokenType.OpenBrace:
                    return ParseBlockExpression();

                case TokenType.KwIf:
                    return ParseIfStatement();

                case TokenType.KwSwitch:
                    return ParseSwitchStatement();
                case TokenType.KwCase:
                    return ParseCaseStatement();

                case TokenType.OpenParen:
                    return ParseTupleExpression(inInfo, ref outInfo);

                case TokenType.KwDelegate:
                    return ParseDelegateDeclaration();

                case TokenType.KwStruct:
                    return ParseStructDeclaration();

                case TokenType.KwEnum:
                    return ParseEnumDeclaration();

                case TokenType.KwInterface:
                case TokenType.KwClass:
                    return ParseClassDeclaration();

                // custom shite
                case TokenType.KwPublic:
                case TokenType.KwInternal:
                case TokenType.KwProtected:
                case TokenType.KwPrivate:
                case TokenType.KwUnreflected:
                    return ParseAccessKeys(token.Type);

                case TokenType.KwAsync:
                    return ParseSyncKeys(token.Type);

                case TokenType.KwConst:
                case TokenType.KwStatic:
                    return ParseInstancingKeys(token.Type);

                case TokenType.KwAbstract:
                case TokenType.KwVirtual:
                case TokenType.KwOverride:
                case TokenType.KwPartial:
                case TokenType.KwExtern:
                case TokenType.KwSealed:
                    return ParseImplementationKeys(token.Type);

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
