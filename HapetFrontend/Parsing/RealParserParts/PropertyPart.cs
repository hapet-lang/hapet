using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstDeclaration PreparePropertyDecl(UnknownDecl udecl, string docString)
        {
            bool hasGet = false;
            bool hasSet = false;
            AstBlockExpr getBody = null;
            AstBlockExpr setBody = null;
            AstStatement initializer = null;

            // getting beginning of the propa
            TokenLocation beg = udecl.Beginning;
            TokenLocation end = beg;

            Consume(TokenType.OpenBrace, ErrMsg("symbol '{'", "at beginning of property declaration"));
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
                    getBody = ParseBlockExpression();
                }
                else
                {
                    ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.UnexpectedTokenAfterGet));
                }
                SkipNewlines();
            }
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
                    setBody = ParseBlockExpression();
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
            SkipNewlines();

            // property initializer
            if (CheckToken(TokenType.Equal))
            {
                NextToken();
                initializer = ParseExpression(true);
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
            theProperty.SpecialKeys.AddRange(udecl.SpecialKeys);

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

            return theProperty;
        }
    }
}
