using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using System.Reflection;
using System.Runtime;
using System.Xml.Linq;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        public AstStatement ParseEmptyExpression(ParserInInfo inInfo)
        {
            var loc = GetWhitespaceLocation(inInfo);
            return new AstEmptyStmt(new Location(loc.beg, loc.end));
        }

        private AstNestedExpr ParseIdentifierExpression(ParserInInfo inInfo, TokenType identType = TokenType.Identifier,
            bool allowDots = true, bool allowGenerics = true, AstNestedExpr iniNested = null, bool expectIdent = false,
            bool allowTupled = false)
        {
            var ident = ParseIdentifierExpressionInternal(inInfo, identType, allowDots, allowGenerics, iniNested, expectIdent);

            // handling cringe like 'var a, b, c = ...'
            bool isComma = CheckToken(inInfo, TokenType.Comma);
            if (allowTupled && isComma)
            {
                List<AstIdExpr> names = new List<AstIdExpr>() { ident.RightPart as AstIdExpr };
                while (isComma)
                {
                    var _ = NextToken(inInfo);
                    var another = ParseIdentifierExpressionInternal(inInfo, allowGenerics: false, allowDots: false);
                    names.Add(another.RightPart as AstIdExpr);

                    isComma = CheckToken(inInfo, TokenType.Comma);
                }
                var tupled = new AstIdTupledExpr(names, new Location(names.First().Beginning, names.Last().Ending));
                ident.RightPart = tupled;
            }
            return ident;
        }

        private AstNestedExpr ParseIdentifierExpressionInternal(ParserInInfo inInfo, TokenType identType = TokenType.Identifier, 
            bool allowDots = true, bool allowGenerics = true, AstNestedExpr iniNested = null, bool expectIdent = false)
        {
            var next = PeekToken(inInfo);
            if (next.Type != identType)
            {
                var customMessage = inInfo.Message ?? new MessageResolver() { MessageArgs = [], XmlMessage = ErrorCode.Get(CTEN.CommonIdentifierExpected) };
                // do not error when look ahead :)
                if (!inInfo.IsLookAheadParsing)
                    ReportMessage(next.Location, customMessage.MessageArgs, customMessage.XmlMessage);
                return new AstNestedExpr(null, iniNested, next.Location);
            }

            var _ = NextToken(inInfo);

            var beg = next.Location.Beginning;
            var currNested = new AstNestedExpr(new AstIdExpr((string)next.Data, new Location(next.Location)), iniNested, next.Location);

            if (allowGenerics && HandleGenericWithLookAhead(inInfo, currNested.RightPart as AstIdExpr, out var genId2))
                currNested.RightPart = genId2;

            // do not skip any lines if directive is parsing
            // all directives should be one-lined
            if (!inInfo.CurrentlyParsingDirective)
            {
                // skip new lines
                SkipNewlines(inInfo);
            }

            // while there are more idents or periods
            while (CheckToken(inInfo, TokenType.Period))
            {
                if (!allowDots)
                {
                    // do not error when look ahead and dots allowed :)
                    if (!inInfo.IsLookAheadParsing)
                        ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.CommonDotUnexpected));
                    currNested.RightPart = null; // wrong parse
                    return currNested;
                }

                var __ = NextToken(inInfo);

                // do not skip any lines if directive is parsing
                // all directives should be one-lined
                if (!inInfo.CurrentlyParsingDirective)
                {
                    // skip new lines
                    SkipNewlines(inInfo);
                }

                if (CheckToken(inInfo, identType))
                {
                    next = NextToken(inInfo);
                    var dt = new AstIdExpr((string)next.Data, new Location(next.Location));
                    currNested = new AstNestedExpr(dt, currNested, new Location(beg, next.Location));
                }
                else
                {
                    // do not error when look ahead :)
                    if (!inInfo.IsLookAheadParsing)
                        ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.CommonIdentAfterDot));
                    currNested.RightPart = null; // wrong parse
                    return currNested;
                }

                if (allowGenerics && HandleGenericWithLookAhead(inInfo, currNested.RightPart as AstIdExpr, out var genId))
                    currNested.RightPart = genId;
            }

            // make this shite as ident with additional info
            if (currNested.RightPart is AstIdExpr idExpr && expectIdent)
            {
                idExpr.AdditionalData = currNested.LeftPart;
            }

            return currNested;
        }

        private bool HandleGenericWithLookAhead(ParserInInfo inInfo, AstIdExpr idExpr, out AstIdGenericExpr gener)
        {
            if (!inInfo.IsLookAheadParsing)
                UpdateLookAheadLocation();
            SaveLookAheadLocation();
            var savedLookAhead = inInfo.IsLookAheadParsing;
            inInfo.IsLookAheadParsing = true;
            // check for generic call
            if (CheckToken(inInfo, TokenType.Less))
            {
                bool isGeneric = HandleGeneric(inInfo, idExpr, out var aheadGenId);

                SkipNewlines(inInfo);
                if (!CheckToken(inInfo, TokenType.OpenParen) &&  // when Anime<T>(..)
                    !CheckToken(inInfo, TokenType.OpenBrace) &&  // when Anime<T> { ...
                    !CheckToken(inInfo, TokenType.CloseParen) && // when (Anime<T>)inst
                    !CheckToken(inInfo, TokenType.Greater) &&    // when ...<Anime<int>>
                    !CheckToken(inInfo, TokenType.NewLine) &&    // when Anime<int>\n
                    !CheckToken(inInfo, TokenType.Colon) &&      // when Anime<int> : ...
                    !CheckToken(inInfo, TokenType.Comma) &&      // when Anime<int>, ...
                    !CheckToken(inInfo, TokenType.Period) &&     // when Anime<int>.Anime2 ...
                    !CheckToken(inInfo, TokenType.KwWhere) &&    // when Anime<T> where T ...
                    !CheckToken(inInfo, TokenType.Semicolon) &&  // when a = abs.Anime<T>; - generic prop
                    !CheckToken(inInfo, TokenType.Asterisk) &&   // when a = abs.Anime<T>*;
                    !CheckToken(inInfo, TokenType.KwOperator) && // when Anime<T> operator ...;
                    !CheckToken(inInfo, TokenType.EOF))          // :)
                {
                    var tmp = PeekToken(inInfo);
                    if (CheckToken(inInfo, TokenType.Identifier))
                    {
                        // this is hard to understand, it could be:
                        // public Anime<T> GetAnime(...
                        // public Anime<T> GetAnime = new Anime<T>();
                        // public Anime<T> GetAnime;
                        // public void GetAnime(Anime<T> aaa)...
                        // so additional checks are required :)

                        // eat the identifier
                        // do not allow dots - if it is a pointer - then
                        // the second ident is a name
                        var nextNest = ParseIdentifierExpressionInternal(inInfo, allowDots: true, allowGenerics: true, expectIdent: true);
                        SkipNewlines(inInfo);
                        if (nextNest.RightPart == null || (
                            !CheckToken(inInfo, TokenType.Equal) &&        // when public Anime<T> GetAnime = new Anime<T>();
                            !CheckToken(inInfo, TokenType.Semicolon) &&    // when public Anime<T> GetAnime;
                            !CheckToken(inInfo, TokenType.Comma) &&        // when (Anime<T> GetAnime, ...)
                            !CheckToken(inInfo, TokenType.OpenParen) &&    // when public Anime<T> GetAnime(...
                            !CheckToken(inInfo, TokenType.Arrow) &&        // when public Anime<T> GetAnime => ...
                            !CheckToken(inInfo, TokenType.OpenBrace) &&    // when public Anime<T> GetAnime { ...
                            !CheckToken(inInfo, TokenType.Less) &&         // when public Anime<T> GetAnime<...- fucking explicit impls
                            !CheckToken(inInfo, TokenType.Period) &&       // when public Anime<T> GetAnime<T>.Func - fucking explicit impls
                            !CheckToken(inInfo, TokenType.CloseParen)))    // when public void GetAnime(Anime<T> aaa)...
                        {
                            if (inInfo.PreferGenericShite && (
                                CheckToken(inInfo, TokenType.LogicalAnd) || // when 'obj is ValueTuple<T1, T2> tuple && ...'
                                CheckToken(inInfo, TokenType.LogicalOr)))   // when 'obj is ValueTuple<T1, T2> tuple || ...'
                            {
                                // empty :)
                            }
                            else
                            {
                                // cringe, it is not generic shite - skip
                                isGeneric = false;
                            }
                        }
                        // else is ok :)
                    }
                    else
                    {
                        // cringe, it is not generic shite - skip
                        isGeneric = false;
                    }
                }

                // if really generic shite
                if (isGeneric)
                {
                    RestoreLookAheadLocation();
                    inInfo.IsLookAheadParsing = savedLookAhead;

                    // parse normally
                    // creating the generic ast id
                    HandleGeneric(inInfo, idExpr, out AstIdGenericExpr genId);

                    gener = genId;
                    return true;
                }
            }
            RestoreLookAheadLocation();
            inInfo.IsLookAheadParsing = savedLookAhead;

            gener = null;
            return false;
        }

        private bool HandleGeneric(ParserInInfo inInfo, AstIdExpr originId, out AstIdGenericExpr gener)
        {
            gener = null;

            if (!CheckToken(inInfo, TokenType.Less))
                return false;
            var _ = NextToken(inInfo);

            List<AstExpression> generics = new List<AstExpression>();
            // <Anime, dwdawd.dasd, ...>
            while (CheckToken(inInfo, TokenType.Identifier))
            {
                generics.Add(ParseIdentifierExpressionInternal(inInfo, allowGenerics: true));

                // just skip commas
                if (CheckToken(inInfo, TokenType.Comma))
                {
                    var __ = NextToken(inInfo);
                }
            }

            var nxt = NextToken(inInfo);
            if (nxt.Type != TokenType.Greater)
            {
                var custom = ErrMsg(">", "after generic types");
                if (!inInfo.IsLookAheadParsing)
                    ReportMessage(nxt.Location, custom.MessageArgs, custom.XmlMessage);

                return false;
            }

            gener = AstIdGenericExpr.FromAstIdExpr(originId, generics);
            return true;
        }

        private bool IsThatPointerWithLookAhead(ParserInInfo inInfo, bool isMultiplyAllowed)
        {
            // lookahead cringe
            // should be done only once
            if (!inInfo.IsLookAheadParsing)
                UpdateLookAheadLocation();
            SaveLookAheadLocation();
            var savedLookAhead = inInfo.IsLookAheadParsing;
            inInfo.IsLookAheadParsing = true;

            // if it is not an asterisk - of course there is no pointer
            if (!CheckToken(inInfo, TokenType.Asterisk))
            {
                RestoreLookAheadLocation();
                inInfo.IsLookAheadParsing = savedLookAhead;
                return false;
            }

            NextToken(inInfo);
            bool isPointer = false;

            SkipNewlines(inInfo);
            if (CheckToken(inInfo, TokenType.Identifier))
            {
                // this is hard to understand, it could be:
                // byte* test ...
                // test * test2 ...
                // so additional checks are required :)

                // eat the identifier
                // do not allow dots - if it is a pointer - then
                // the second ident is a name
                var nextNest = ParseIdentifierExpressionInternal(inInfo, allowDots: true, allowGenerics: true);
                SkipNewlines(inInfo);
                if (nextNest.RightPart != null && (
                    CheckToken(inInfo, TokenType.Equal) ||        // when byte* aaa = ...
                    CheckToken(inInfo, TokenType.OpenBrace) ||    // when byte* Aaa { ...
                    CheckToken(inInfo, TokenType.Arrow) ||        // when byte* Aaa => ...
                    CheckToken(inInfo, TokenType.OpenParen)))     // when public byte* aaa(...
                {
                    isPointer = true;
                }

                // 50/50 cases
                if (CheckToken(inInfo, TokenType.Semicolon) ||    // when 'byte* aaa;' OR 'anime = test * test;'
                    CheckToken(inInfo, TokenType.CloseParen) ||   // when '(byte* aaa)' OR 'anime(test * test)'
                    CheckToken(inInfo, TokenType.OpenBracket) ||  // when 'byte* this[int i]' OR 'test * test[i]'
                    CheckToken(inInfo, TokenType.Comma))          // when '(byte* aaa, ...)' OR 'anime(test * test, ...)'
                {
                    if (!isMultiplyAllowed)
                        isPointer = true;
                }
            }
            else if (CheckToken(inInfo, TokenType.CloseParen) ||  // when (Anime*)inst
                    CheckToken(inInfo, TokenType.OpenBracket) ||  // when a = new Anime*[..]; 
                    CheckToken(inInfo, TokenType.Semicolon) ||    // when a = Anime*;
                    CheckToken(inInfo, TokenType.Asterisk) ||     // when a = Anime**
                    CheckToken(inInfo, TokenType.KwOperator) ||   // when Anime* operator ...
                    CheckToken(inInfo, TokenType.Greater) ||      // when Cringe<Anime*>
                    CheckToken(inInfo, TokenType.EOF))            // :)
            {
                isPointer = true;
            }
            else
            {
                // when Anime * (..)
                // when Anime * 'other exprs'
            }
            RestoreLookAheadLocation();
            inInfo.IsLookAheadParsing = savedLookAhead;
            return isPointer;
        }

        private void CheckSemicolonAfterStmt(ParserInInfo inInfo, AstStatement s, bool skipDefault = false)
        {
            // consume semicolon after other stmt
            if (s is not AstWhileStmt &&
                s is not AstForStmt &&
                s is not AstIfStmt &&
                s is not AstCaseStmt &&
                s is not AstSwitchStmt &&
                s is not AstTryCatchStmt &&
                s is not AstCatchStmt &&
                s is not AstFuncDecl &&
                s is not AstBlockExpr &&
                s is not AstClassDecl &&
                s is not AstStructDecl &&
                s is not AstEnumDecl &&
                s is not AstDirectiveStmt)
            {
                // if skip default checks
                if (skipDefault && s is AstDefaultExpr)
                    return;

                if (!CheckToken(inInfo, TokenType.Semicolon))
                {
                    // here u can set breakpoint to catch error
                }
                Consume(inInfo, TokenType.Semicolon, ErrMsg(";", "at the end of the statement"));
            }
        }
    }
}
