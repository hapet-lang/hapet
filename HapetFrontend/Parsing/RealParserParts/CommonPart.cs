using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Errors;
using System.Collections.Generic;
using System.Text;

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
            bool allowDots = true, AstNestedExpr iniNested = null)
        {
            var next = PeekToken();
            if (next.Type != identType)
            {
                customMessage ??= new MessageResolver() { MessageArgs = [], XmlMessage = ErrorCode.Get(CTEN.CommonIdentifierExpected) };
                ReportMessage(next.Location, customMessage.MessageArgs, customMessage.XmlMessage);
                return new AstNestedExpr(new AstIdExpr("anon", new Location(next.Location)), iniNested, next.Location);
            }
            NextToken();

            var beg = next.Location.Beginning;
            var currNested = new AstNestedExpr(new AstIdExpr((string)next.Data, new Location(next.Location)), iniNested, next.Location);

            // while there are more idents or periods
            while (CheckToken(TokenType.Period))
            {
                if (!allowDots)
                {
                    ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.CommonDotUnexpected));
                }

                NextToken();
                if (CheckToken(identType))
                {
                    next = NextToken();
                    var dt = new AstIdExpr((string)next.Data, new Location(next.Location));
                    currNested = new AstNestedExpr(dt, currNested, new Location(beg, next.Location));
                }
                else
                {
                    ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.CommonIdentAfterDot));
                }
            }

            return currNested;
        }

        private AstDeclaration PrepareUnknownDecl(UnknownDecl udecl, string docString, bool allowCommaTuple, List<AstAttributeStmt> attrs)
        {
            TokenLocation end = udecl.Ending;
            AstStatement initializer = null;

            // variable declaration with initializer
            if (CheckToken(TokenType.Equal))
            {
                NextToken();
                initializer = ParseExpression(allowCommaTuple);
                end = initializer.Ending;

                if (initializer is not AstExpression)
                {
                    ReportMessage(initializer.Location, [], ErrorCode.Get(CTEN.VarIniterExpr));
                }

                var varDecl = new AstVarDecl(udecl.Type, udecl.Name, initializer as AstExpression, docString, Location: new Location(udecl.Beginning, end));
                varDecl.Attributes.AddRange(attrs);
                varDecl.SpecialKeys.AddRange(udecl.SpecialKeys);
                return varDecl;
            }
            // variable declaration without initializer
            else if (CheckToken(TokenType.Semicolon))
            {
                // do not get the next token
                var varDecl = new AstVarDecl(udecl.Type, udecl.Name, null, docString, Location: new Location(udecl.Beginning, end));
                varDecl.Attributes.AddRange(attrs);
                varDecl.SpecialKeys.AddRange(udecl.SpecialKeys);
                return varDecl;
            }
            // func declaration 
            else if (CheckToken(TokenType.OpenParen))
            {
                var tpl = ParseTupleExpression(true, true);
                if (tpl is AstFuncDecl func)
                {
                    if (udecl.Type == null)
                    {
                        // it is ctor/dtor
                        // func.Name = udecl.Name.GetCopy(udecl.Name.Name + (udecl.Name.Suffix != "~" ? "_ctor" : "_dtor")); // no need anymore?
                        func.Name = udecl.Name.GetCopy();
                        func.Returns = new AstIdExpr("void");
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
                    return func;
                }
                // TODO: could there be a lambda???
            }
            // properties 
            else if (CheckToken(TokenType.OpenBrace))
            {
                var prop = PreparePropertyDecl(udecl, docString);
                prop.Attributes.AddRange(attrs);
                // special keys are added inside PreparePropertyDecl
                return prop;
            }

            // possible operator override
            var result = ParseOperatorOverride(udecl);
            if (result != null)
            {
                result.Attributes.AddRange(attrs);
                return result;
            }

            ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.PureUnexpectedToken)); // TODO: better error message?
            return udecl;
        }
    }
}
