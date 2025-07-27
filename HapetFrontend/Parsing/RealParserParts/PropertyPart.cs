using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Entities;
using System.Diagnostics;
using HapetFrontend.Helpers;
using HapetFrontend.Ast.Statements;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstDeclaration PreparePropertyDecl(AstUnknownDecl udecl, string docString, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            bool hasGet = false;
            bool hasSet = false;
            AstBlockExpr getBody = null;
            AstBlockExpr setBody = null;
            AstStatement initializer = null;

            // required!!! must not be null!!!
            ArgumentNullException.ThrowIfNull(udecl);

            List<AstIdExpr> generics = new List<AstIdExpr>();
            // getting generics from parsed udecl' name
            if (udecl.Name is AstIdGenericExpr genExpr)
            {
                generics = GenericsHelper.GetGenericsFromName(genExpr, _messageHandler);
            }

            // parsing constrains
            var genericConstrains = ParseGenericConstrains(generics);

            // getting beginning of the propa
            TokenLocation beg = udecl.Beginning;
            TokenLocation end = beg;

            List<Token> getSpecialKeys = new List<Token>();
            List<Token> setSpecialKeys = new List<Token>();

            if (CheckToken(TokenType.Arrow))
            {
                // => property

                NextToken();
                // getting only one stmt if there are no braces
                var onlyExpr = ParseExpression(inInfo, ref outInfo);
                onlyExpr = new AstReturnStmt(onlyExpr as AstExpression, onlyExpr.Location);
                var body = new AstBlockExpr(new List<AstStatement>() { onlyExpr }, onlyExpr);

                // try eat semicolon or error
                CheckSemicolonAfterStmt(onlyExpr);
                getBody = body;
            }
            else
            {
                // { get; set; } property

                Consume(TokenType.OpenBrace, ErrMsg("symbol '{'", "at beginning of property declaration"));
                SkipNewlines();

                // special keys of 'get'
                getSpecialKeys = ParseSpecialKeys();
                SkipNewlines();

                // if it has 'get'
                if (CheckToken(TokenType.KwGet))
                {
                    Consume(TokenType.KwGet, ErrMsg("keyword 'get'", "..."));
                    SkipNewlines();
                    hasGet = true;

                    // check what is going next
                    if (CheckToken(TokenType.Semicolon))
                    {
                        // no body here
                        Consume(TokenType.Semicolon, ErrMsg("symbol ';'", "after 'get'"));
                    }
                    else if (CheckToken(TokenType.OpenBrace))
                    {
                        // the 'get' block
                        getBody = ParseBlockExpression(inInfo, ref outInfo);
                    }
                    else
                    {
                        ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.UnexpectedTokenAfterGet));
                    }
                    SkipNewlines();
                }
                // special keys of 'set'
                setSpecialKeys = ParseSpecialKeys();
                SkipNewlines();
                if (CheckToken(TokenType.KwSet))
                {
                    Consume(TokenType.KwSet, ErrMsg("keyword 'set'", "..."));
                    SkipNewlines();
                    hasSet = true;

                    // check what is going next
                    if (CheckToken(TokenType.Semicolon))
                    {
                        // no body here
                        Consume(TokenType.Semicolon, ErrMsg("symbol ';'", "after 'set'"));
                    }
                    else if (CheckToken(TokenType.OpenBrace))
                    {
                        // the 'set' block
                        setBody = ParseBlockExpression(inInfo, ref outInfo);
                    }
                    else
                    {
                        ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.UnexpectedTokenAfterSet));
                    }
                    SkipNewlines();

                    // check if 'get' goes after 'set' and error
                    if (CheckToken(TokenType.KwGet))
                    {
                        ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.GetNotBeforeSet));
                    }
                }
                // end of propa
                end = Consume(TokenType.CloseBrace, ErrMsg("symbol '}'", "at end of property declaration")).Location;
            }

            SkipNewlines();

            // property initializer
            if (CheckToken(TokenType.Equal))
            {
                NextToken();
                initializer = ParseExpression(inInfo, ref outInfo);

                if (initializer is not AstExpression)
                {
                    ReportMessage(initializer.Location, [], ErrorCode.Get(CTEN.PropIniNotExpr));
                }
            }

            // creating the property ast
            var theProperty = new AstPropertyDecl(udecl.Type, udecl.Name, initializer as AstExpression, docString, new Location(beg, end));
            theProperty.HasGet = hasGet;
            theProperty.HasSet = hasSet;
            theProperty.GetBlock = getBody;
            theProperty.SetBlock = setBody;
            theProperty.HasGenericTypes = generics.Count > 0;
            theProperty.GenericConstrains = genericConstrains;
            theProperty.GetSpecialKeys.AddRange(getSpecialKeys);
            theProperty.SetSpecialKeys.AddRange(setSpecialKeys);
            theProperty.SpecialKeys.AddRange(udecl.SpecialKeys);
            theProperty.IsImported = inInfo.ExternalMetadata;

            // do some checks because they could be done here, not in pp
            if (!hasGet && hasSet && setBody == null)
            {
                // the case is 'Prop { set; }' so we should error
                ReportMessage(theProperty.Location, [], ErrorCode.Get(CTEN.NoGetAndNoSetBody));
            }
            else if (hasGet && hasSet && getBody == null && setBody != null)
            {
                // the case is 'Prop { get; set {...} }' so we should error
                ReportMessage(theProperty.Location, [], ErrorCode.Get(CTEN.ExpectedGetBody));
            }
            else if (hasGet && hasSet && getBody != null && setBody == null)
            {
                // the case is 'Prop { get {...} set; }' so we should error
                ReportMessage(theProperty.Location, [], ErrorCode.Get(CTEN.ExpectedSetBody));
            }

            // do some checks for generics
            if (theProperty.HasGenericTypes && (!hasGet || getBody == null || (hasSet && setBody == null)))
            {
                // the case is
                // 'Prop<T> { get; set; }' or
                // 'Prop<T> { get; }' or
                // 'Prop<T> { get {...} set; }'
                // so we should error
                ReportMessage(theProperty.Location, [], ErrorCode.Get(CTEN.PropGenericWithoutBody));
            }

            return theProperty;
        }
    }
}
