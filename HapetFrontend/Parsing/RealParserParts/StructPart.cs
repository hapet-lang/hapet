using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstDeclaration ParseStructDeclaration(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            TokenLocation beg = null, end = null;
            var declarations = new List<AstDeclaration>();
            var inherited = new List<AstNestedExpr>();
            var generics = new List<AstIdExpr>();
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
                var nest = ParseIdentifierExpression(inInfo, allowDots: false, allowGenerics: true);
                if (nest.RightPart is not AstIdExpr idExpr)
                {
                    ReportMessage(nest.Location, [], ErrorCode.Get(CTEN.StructNameNotIdent));
                    return new AstStructDecl(new AstIdExpr("unknown"), declarations, "", beg);
                }
                structName = idExpr;
            }

            // checking generics
            // getting generics from parsed struct name
            if (structName is AstIdGenericExpr genExpr)
            {
                generics = GenericsHelper.GetGenericsFromName(genExpr, _messageHandler);
            }

            SkipNewlines();

            // checking for inheritance
            if (CheckToken(TokenType.Colon))
            {
                Consume(TokenType.Colon, ErrMsg(":", "before inherited types"));
                SkipNewlines();

                while (CheckToken(TokenType.Identifier))
                {
                    var ident = ParseIdentifierExpression(inInfo, allowGenerics: true);
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

            // parsing constrains
            var genericConstrains = ParseGenericConstrains(generics);

            // the decl (declared here because it would be used)
            var structDecl = new AstStructDecl(structName, declarations, "", null);

            ConsumeUntil(TokenType.OpenBrace, ErrMsg("{", "at beginning of struct body"), true);

            SkipNewlines();
            while (true)
            {
                var next = PeekToken();
                if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                    break;

                var decl = ParseTopLevel(inInfo, ref outInfo);
                if (decl is not AstDeclaration realDecl)
                {
                    ReportMessage(decl, ["class"], ErrorCode.Get(CTEN.DeclExpectedInClassStruct));
                    continue;
                }

                // mark the decl as nested if it is:
                if (realDecl is AstClassDecl ||
                    realDecl is AstStructDecl ||
                    realDecl is AstEnumDecl ||
                    realDecl is AstDelegateDecl)
                {
                    realDecl.IsNestedDecl = true;
                    realDecl.ParentDecl = structDecl;
                }

                // error - unexpected decl in struct type
                if (decl is not AstVarDecl && 
                    decl is not AstFuncDecl &&
                    decl is not AstClassDecl &&
                    decl is not AstStructDecl)
                {
                    ReportMessage(decl, [], ErrorCode.Get(CTEN.UnexpectedDeclInStruct));
                }

                declarations.Add(realDecl);

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
            }

            end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of struct declaration")).Location;

            structDecl.Location = new Location(beg, end);
            structDecl.InheritedFrom = inherited;
            structDecl.HasGenericTypes = generics.Count > 0;
            structDecl.GenericConstrains = genericConstrains;
            structDecl.IsImported = inInfo.ExternalMetadata;
            return structDecl;
        }
    }
}
