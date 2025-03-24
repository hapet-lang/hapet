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
            bool allowDots = true, bool allowGenerics = false, AstNestedExpr iniNested = null, bool lookAhead = false)
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
                if (allowGenerics && HandleGeneric(currNested.RightPart as AstIdExpr, out var genId2, lookAhead))
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
            if (allowGenerics && HandleGeneric(currNested.RightPart as AstIdExpr, out var genId, lookAhead))
                currNested.RightPart = genId;

            return currNested;
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

        private AstDeclaration PrepareUnknownDecl(AstUnknownDecl udecl, List<AstAttributeStmt> attrs, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            TokenLocation end = udecl.Ending;
            AstStatement initializer = null;
            var savedUdecl = inInfo.CurrentUdecl;
            inInfo.CurrentUdecl = udecl;

            // variable declaration with initializer
            if (CheckToken(TokenType.Equal))
            {
                NextToken();
                initializer = ParseExpression(inInfo, ref outInfo);
                end = initializer.Ending;

                if (initializer is not AstExpression)
                {
                    ReportMessage(initializer.Location, [], ErrorCode.Get(CTEN.VarIniterExpr));
                }

                var varDecl = new AstVarDecl(udecl.Type, udecl.Name, initializer as AstExpression, udecl.Documentation, new Location(udecl.Beginning, end));
                varDecl.Attributes.AddRange(attrs);
                varDecl.SpecialKeys.AddRange(udecl.SpecialKeys);
                varDecl.IsImported = inInfo.ExternalMetadata;
                OnExit();
                return varDecl;
            }
            // variable declaration without initializer
            else if (CheckToken(TokenType.Semicolon))
            {
                // do not get the next token
                var varDecl = new AstVarDecl(udecl.Type, udecl.Name, null, udecl.Documentation, new Location(udecl.Beginning, end));
                varDecl.Attributes.AddRange(attrs);
                varDecl.SpecialKeys.AddRange(udecl.SpecialKeys);
                varDecl.IsImported = inInfo.ExternalMetadata;
                OnExit();
                return varDecl;
            }
            // func declaration 
            else if (CheckToken(TokenType.OpenParen))
            {
                var saved1 = inInfo.AllowFunctionDeclaration;
                var saved2 = inInfo.AllowCommaForTuple;
                inInfo.AllowFunctionDeclaration = true;
                inInfo.AllowCommaForTuple = true;
                var tpl = ParseTupleExpression(inInfo, ref outInfo);
                inInfo.AllowFunctionDeclaration = saved1;
                inInfo.AllowCommaForTuple = saved2;

                if (tpl is AstFuncDecl func)
                {
                    if (udecl.Type == null)
                    {
                        // it is ctor/dtor
                        // func.Name = udecl.Name.GetCopy(udecl.Name.Name + (udecl.Name.Suffix != "~" ? "_ctor" : "_dtor")); // no need anymore?
                        func.Name = udecl.Name.GetCopy();
                        func.Returns = new AstNestedExpr(new AstIdExpr("void"), null);
                        // check that it is a static ctor
                        if (udecl.Name.Suffix != "~" && udecl.SpecialKeys.Contains(TokenType.KwStatic))
                            func.ClassFunctionType = Enums.ClassFunctionType.StaticCtor;
                        else
                            func.ClassFunctionType = udecl.Name.Suffix != "~" ? Enums.ClassFunctionType.Ctor : Enums.ClassFunctionType.Dtor;
                    }
                    else
                    {
                        // it is normal func
                        func.Name = udecl.Name;
                        func.Returns = udecl.Type;
                    }
                    func.Attributes.AddRange(attrs);
                    func.SpecialKeys.AddRange(udecl.SpecialKeys);
                    OnExit();
                    return func;
                }
                // TODO: could there be a lambda???
            }
            // properties 
            else if (CheckToken(TokenType.OpenBrace))
            {
                var prop = PreparePropertyDecl(udecl, udecl.Documentation);
                prop.Attributes.AddRange(attrs);
                // special keys are added inside PreparePropertyDecl
                OnExit();
                return prop;
            }

            // possible operator override
            var result = ParseOperatorOverride(udecl);
            if (result != null)
            {
                result.Attributes.AddRange(attrs);
                OnExit();
                return result;
            }

            ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.PureUnexpectedToken)); // TODO: better error message?
            OnExit();
            return udecl;

            void OnExit()
            {
                inInfo.CurrentUdecl = savedUdecl;
            }
        }
    }
}
