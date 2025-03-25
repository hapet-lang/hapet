using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        public AstStatement ParseEmptyExpression()
        {
            var loc = GetWhitespaceLocation();
            return new AstEmptyStmt(new Location(loc.beg, loc.end));
        }

        private AstNestedExpr ParseIdentifierExpression(MessageResolver customMessage = null, TokenType identType = TokenType.Identifier, 
            bool allowDots = true, bool allowGenerics = true, AstNestedExpr iniNested = null, bool lookAhead = false)
        {
            var next = lookAhead ? PeekLookAhead() : PeekToken();
            if (next.Type != identType)
            {
                customMessage ??= new MessageResolver() { MessageArgs = [], XmlMessage = ErrorCode.Get(CTEN.CommonIdentifierExpected) };
                // do not error when look ahead :)
                if (!lookAhead)
                    ReportMessage(next.Location, customMessage.MessageArgs, customMessage.XmlMessage);
                return new AstNestedExpr(null, iniNested, next.Location);
            }

            var _ = lookAhead ? NextLookAhead() : NextToken();

            var beg = next.Location.Beginning;
            var currNested = new AstNestedExpr(new AstIdExpr((string)next.Data, new Location(next.Location)), iniNested, next.Location);

            // while there are more idents or periods
            while (CheckToken(TokenType.Period))
            {
                if (allowGenerics && HandleGenericWithLookAhead(currNested.RightPart as AstIdExpr, out var genId2, lookAhead))
                    currNested.RightPart = genId2;

                if (!allowDots)
                {
                    // do not error when look ahead and dots allowed :)
                    if (!lookAhead)
                        ReportMessage((lookAhead ? PeekLookAhead() : PeekToken()).Location, [], ErrorCode.Get(CTEN.CommonDotUnexpected));
                    currNested.RightPart = null; // wrong parse
                    return currNested;
                }

                var __ = lookAhead ? NextLookAhead() : NextToken();
                if ((lookAhead ? CheckLookAhead(identType) : CheckToken(identType)))
                {
                    next = lookAhead ? NextLookAhead() : NextToken();
                    var dt = new AstIdExpr((string)next.Data, new Location(next.Location));
                    currNested = new AstNestedExpr(dt, currNested, new Location(beg, next.Location));
                }
                else
                {
                    // do not error when look ahead :)
                    if (!lookAhead)
                        ReportMessage((lookAhead ? PeekLookAhead() : PeekToken()).Location, [], ErrorCode.Get(CTEN.CommonIdentAfterDot));
                    currNested.RightPart = null; // wrong parse
                    return currNested;
                }
            }
            if (allowGenerics && HandleGenericWithLookAhead(currNested.RightPart as AstIdExpr, out var genId, lookAhead))
                currNested.RightPart = genId;

            return currNested;
        }

        private bool HandleGenericWithLookAhead(AstIdExpr idExpr, out AstIdGenericExpr gener, bool lookAhead = false)
        {
            // check for generic call
            if (CheckToken(TokenType.Less))
            {
                // lookahead cringe
                UpdateLookAheadLocation();
                bool isGeneric = HandleGeneric(idExpr, out var aheadGenId, true);

                if (!CheckLookAhead(TokenType.OpenParen) &&  // when Anime<T>(..)
                    !CheckLookAhead(TokenType.CloseParen) && // when (Anime<T>)inst
                    !CheckLookAhead(TokenType.Semicolon) &&  // when a = abs.Anime<T>; - generic prop
                    !CheckLookAhead(TokenType.EOF))          // :)
                {
                    // cringe, it is not generic shite - skip
                    isGeneric = false;
                }

                // if really generic shite
                if (isGeneric)
                {
                    AstIdGenericExpr genId;
                    // and not only lookahead - parse normally
                    if (!lookAhead)
                        // creating the generic ast id
                        HandleGeneric(idExpr, out genId, false);
                    else
                        genId = aheadGenId;

                    gener = genId;
                    return true;
                }
            }
            gener = null;
            return false;
        }

        private bool HandleGeneric(AstIdExpr originId, out AstIdGenericExpr gener, bool lookAhead = false)
        {
            gener = null;

            if (!(lookAhead ? CheckLookAhead(TokenType.Less) : CheckToken(TokenType.Less)))
                return false;
            var _ = lookAhead ? NextLookAhead() : NextToken();

            List<AstExpression> generics = new List<AstExpression>();
            // <Anime, dwdawd.dasd, ...>
            while ((lookAhead ? CheckLookAhead(TokenType.Identifier) : CheckToken(TokenType.Identifier)))
            {
                generics.Add(ParseIdentifierExpression(allowGenerics: true, lookAhead: lookAhead));

                // just skip commas
                if ((lookAhead ? CheckLookAhead(TokenType.Comma) : CheckToken(TokenType.Comma)))
                {
                    var __ = lookAhead ? NextLookAhead() : NextToken();
                }
            }

            var nxt = lookAhead ? NextLookAhead() : NextToken();
            if (nxt.Type != TokenType.Greater)
            {
                var custom = ErrMsg(">", "after generic types");
                if (!lookAhead)
                    ReportMessage(nxt.Location, custom.MessageArgs, custom.XmlMessage);

                return false;
            }

            gener = AstIdGenericExpr.FromAstIdExpr(originId, generics);
            return true;
        }

        private bool IsThatPointerWithLookAhead(AstNestedExpr nest)
        {
            // lookahead cringe
            UpdateLookAheadLocation();

            // if it is not an asterisk - of course there is no pointer
            if (!CheckLookAhead(TokenType.Asterisk, false))
                return false;

            NextLookAhead(false);
            bool isPointer = false;

            if (CheckLookAhead(TokenType.Identifier))
            {
                // this is hard to understand, it could be:
                // byte* test ...
                // test * test2 ...
                // so additional checks are required :)

                // eat the identifier
                // do not allow dots - if it is a pointer - then
                // the second ident is a name
                var nextNest = ParseIdentifierExpression(lookAhead: true, allowDots: false);
                if (nextNest.RightPart != null && (
                    CheckLookAhead(TokenType.Equal) ||        // when byte* aaa = ...
                    CheckLookAhead(TokenType.Semicolon) ||    // when byte* aaa;
                    CheckLookAhead(TokenType.CloseParen)))    // when (byte* aaa)
                {
                    isPointer = true;
                }
            }
            else if (CheckLookAhead(TokenType.CloseParen) ||  // when (Anime*)inst
                    CheckLookAhead(TokenType.OpenBracket) ||  // when a = new Anime*[..]; 
                    CheckLookAhead(TokenType.Semicolon) ||    // when a = Anime*;
                    CheckLookAhead(TokenType.EOF))            // :)
            {
                isPointer = true;
            }
            else
            {
                // when Anime * (..)
                // when Anime * 'other exprs'
            }
            return isPointer;
        }
    }
}
