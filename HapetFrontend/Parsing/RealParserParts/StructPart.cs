using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstDeclaration ParseStructDeclaration()
        {
            TokenLocation beg = null, end = null;
            var declarations = new List<AstDeclaration>();
            AstIdExpr structName = null;

            beg = Consume(TokenType.KwStruct, ErrMsg("keyword 'struct'", "at beginning of struct type")).Location;

            // struct name
            if (!CheckToken(TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.StructDeclExpectedAfterKey));
            }
            else
            {
                var nest = ParseIdentifierExpression(allowDots: false);
                if (nest.RightPart is not AstIdExpr idExpr)
                {
                    ReportMessage(nest.Location, [], ErrorCode.Get(CTEN.StructNameNotIdent));
                    return new AstStructDecl(new AstIdExpr("unknown"), declarations, "", beg);
                }
                structName = idExpr;
            }
            SkipNewlines();

            ConsumeUntil(TokenType.OpenBrace, ErrMsg("{", "at beginning of struct body"), true);

            SkipNewlines();
            while (true)
            {
                var next = PeekToken();
                if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                    break;

                var decl = ParseDeclaration(null, true);
                // it is probably an attribute so no need to save it to decls
                if (decl is AstEmptyDecl)
                {
                    SkipNewlines();
                    continue;
                }

                // error - unexpected decl in struct type
                if (decl is not AstVarDecl)
                {
                    ReportMessage(decl, [], ErrorCode.Get(CTEN.UnexpectedDeclInStruct));
                }

                declarations.Add(decl);

                next = PeekToken();
                if (next.Type == TokenType.NewLine)
                {
                    SkipNewlines();
                }
                else if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                {
                    break;
                }
                else if (decl is AstVarDecl && next.Type == TokenType.Semicolon)
                {
                    // it is just a ';' at the end of class field
                    NextToken();
                    SkipNewlines();
                }
                else if (decl is not AstVarDecl)
                {
                    NextToken();
                    ReportMessage(decl.Location, [], ErrorCode.Get(CTEN.TheDeclNotAllowedInStruct));
                }
            }

            end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of struct declaration")).Location;

            // TODO: doc string
            return new AstStructDecl(structName, declarations, "", new Location(beg, end));
        }
    }
}
