using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstDeclaration ParseClassDeclaration()
        {
            TokenLocation beg = null, end = null;
            var declarations = new List<AstDeclaration>();
            var inherited = new List<AstNestedExpr>();
            AstIdExpr className = null;
            bool isInterface = false;

            // check if it is an interface decl
            if (CheckToken(TokenType.KwClass))
            {
                beg = Consume(TokenType.KwClass, ErrMsg("keyword 'class'", "at beginning of class type")).Location;
            }
            else
            {
                beg = Consume(TokenType.KwInterface, ErrMsg("keyword 'interface'", "at beginning of interface type")).Location;
                isInterface = true;
            }

            // class name
            if (!CheckToken(TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.ClassNameExpected));
            }
            else
            {
                var nest = ParseIdentifierExpression(allowDots: false);
                if (nest.RightPart is not AstIdExpr idExpr)
                {
                    ReportMessage(nest.Location, [], ErrorCode.Get(CTEN.ClassNameNotIdent));
                    return new AstClassDecl(new AstIdExpr("unknown"), declarations, "", beg) { IsInterface = isInterface };
                }
                className = idExpr;
            }

            // checking for inheritance
            if (CheckToken(TokenType.Colon))
            {
                Consume(TokenType.Colon, ErrMsg(":", "before inherited types"));
                SkipNewlines();

                while (CheckToken(TokenType.Identifier))
                {
                    var ident = ParseIdentifierExpression();
                    inherited.Add(ident);
                    // if there is something else
                    if (CheckToken(TokenType.Comma))
                    {
                        Consume(TokenType.Comma, ErrMsg(",", "before the next inherited type"));
                        continue;
                    }

                    // if there is nothing else
                    break;
                }
            }
            SkipNewlines();

            ConsumeUntil(TokenType.OpenBrace, ErrMsg("{", "at beginning of class body"), true);

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
                else
                {
                    // no need for errors!
                    //NextToken();
                    //ReportMessage(next.Location, $"Unexpected token {next} at end of class member");
                }
            }
            end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of declaration")).Location;

            if (isInterface)
            {
                // check if there are fields - warn user
                var field = declarations.FirstOrDefault(x => x is AstVarDecl && x is not AstPropertyDecl);
                if (field != null)
                    ReportMessage(field.Location, [], ErrorCode.Get(CTWN.FieldsInInterface), reportType: Entities.ReportType.Warning);
            }

            // TODO: doc string
            return new AstClassDecl(className, declarations, "", new Location(beg, end)) { InheritedFrom = inherited, IsInterface = isInterface };
        }
    }
}
