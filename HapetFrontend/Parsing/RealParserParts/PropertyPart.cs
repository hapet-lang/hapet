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

            Token getTkn = null;
            Token setTkn = null;

            List<Token> getSpecialKeys = new List<Token>();
            List<Token> setSpecialKeys = new List<Token>();

            if (CheckToken(inInfo, TokenType.Arrow))
            {
                // => property

                NextToken(inInfo);
                // getting only one stmt if there are no braces
                var onlyExpr = ParseExpression(inInfo, ref outInfo);
                onlyExpr = new AstReturnStmt(onlyExpr as AstExpression, onlyExpr.Location);
                var body = new AstBlockExpr(new List<AstStatement>() { onlyExpr }, onlyExpr);

                // try eat semicolon or error
                CheckSemicolonAfterStmt(inInfo, onlyExpr);
                getBody = body;
                hasGet = true;
            }
            else
            {
                // { get; set; } property

                Consume(inInfo, TokenType.OpenBrace, ErrMsg("symbol '{'", "at beginning of property declaration"));
                SkipNewlines(inInfo);

                // special keys of 'get'
                getSpecialKeys = ParseSpecialKeys(inInfo);
                SkipNewlines(inInfo);

                // if it has 'get'
                if (CheckToken(inInfo, TokenType.KwGet))
                {
                    getTkn = Consume(inInfo, TokenType.KwGet, ErrMsg("keyword 'get'", "..."));
                    SkipNewlines(inInfo);
                    hasGet = true;

                    // check what is going next
                    if (CheckToken(inInfo, TokenType.Semicolon))
                    {
                        // no body here
                        Consume(inInfo, TokenType.Semicolon, ErrMsg("symbol ';'", "after 'get'"));
                    }
                    else if (CheckToken(inInfo, TokenType.OpenBrace))
                    {
                        // the 'get' block
                        getBody = ParseBlockExpression(inInfo, ref outInfo);
                    }
                    else if (CheckToken(inInfo, TokenType.Arrow))
                    {
                        // get => ...
                        NextToken(inInfo);
                        // getting only one stmt if there are no braces
                        var onlyExpr = ParseExpression(inInfo, ref outInfo);
                        onlyExpr = new AstReturnStmt(onlyExpr as AstExpression, onlyExpr.Location);
                        var body = new AstBlockExpr(new List<AstStatement>() { onlyExpr }, onlyExpr);
                        // try eat semicolon or error
                        CheckSemicolonAfterStmt(inInfo, onlyExpr);
                        getBody = body;
                    }
                    else
                    {
                        ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.UnexpectedTokenAfterGet));
                    }
                    SkipNewlines(inInfo);
                }
                // special keys of 'set'
                setSpecialKeys = ParseSpecialKeys(inInfo);
                SkipNewlines(inInfo);
                if (CheckToken(inInfo, TokenType.KwSet))
                {
                    setTkn = Consume(inInfo, TokenType.KwSet, ErrMsg("keyword 'set'", "..."));
                    SkipNewlines(inInfo);
                    hasSet = true;

                    // check what is going next
                    if (CheckToken(inInfo, TokenType.Semicolon))
                    {
                        // no body here
                        Consume(inInfo, TokenType.Semicolon, ErrMsg("symbol ';'", "after 'set'"));
                    }
                    else if (CheckToken(inInfo, TokenType.OpenBrace))
                    {
                        // the 'set' block
                        setBody = ParseBlockExpression(inInfo, ref outInfo);
                    }
                    else if (CheckToken(inInfo, TokenType.Arrow))
                    {
                        // set => ...
                        NextToken(inInfo);
                        // getting only one stmt if there are no braces
                        var onlyStmt = ParseStatement(inInfo, ref outInfo);
                        var body = new AstBlockExpr(new List<AstStatement>() { onlyStmt }, onlyStmt);
                        // try eat semicolon or error
                        CheckSemicolonAfterStmt(inInfo, onlyStmt);
                        setBody = body;
                    }
                    else
                    {
                        ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.UnexpectedTokenAfterSet));
                    }
                    SkipNewlines(inInfo);

                    // check if 'get' goes after 'set' and error
                    if (CheckToken(inInfo, TokenType.KwGet))
                    {
                        ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.GetNotBeforeSet));
                    }
                }
                // end of propa
                end = Consume(inInfo, TokenType.CloseBrace, ErrMsg("symbol '}'", "at end of property declaration")).Location;
            }

            SkipNewlines(inInfo);

            // property initializer
            if (CheckToken(inInfo, TokenType.Equal))
            {
                NextToken(inInfo);
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
            theProperty.GetTokenPosition = getTkn?.Location;
            theProperty.SetTokenPosition = setTkn?.Location;

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

            // add special NoFieldAttribute if there is get or set body exists
            if (getBody != null || setBody != null)
            {
                theProperty.Attributes.Add(new AstAttributeStmt(
                    new AstNestedExpr(new AstIdExpr("System.NoFieldAttribute", theProperty.Location), null, theProperty.Location), 
                    new List<AstArgumentExpr>(), theProperty.Location)
                {
                    IsSyntheticStatement = true,
                });
            }

            return theProperty;
        }
    }
}
