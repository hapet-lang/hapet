using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
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

            beg = Consume(inInfo, TokenType.KwStruct, ErrMsg("keyword 'struct'", "at beginning of struct type")).Location;

            // struct name
            if (!CheckToken(inInfo, TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.StructDeclExpectedAfterKey));
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

            SkipNewlines(inInfo);

            // checking for inheritance
            if (CheckToken(inInfo, TokenType.Colon))
            {
                Consume(inInfo, TokenType.Colon, ErrMsg(":", "before inherited types"));
                SkipNewlines(inInfo);

                while (CheckToken(inInfo, TokenType.Identifier))
                {
                    var ident = ParseIdentifierExpression(inInfo, allowGenerics: true);
                    inherited.Add(ident);
                    // if there is something else
                    if (CheckToken(inInfo, TokenType.Comma))
                    {
                        Consume(inInfo, TokenType.Comma, ErrMsg(",", "before the next inherited type"));
                        continue;
                    }

                    // if there is nothing else
                    break;
                }
            }
            SkipNewlines(inInfo);

            // parsing constrains
            var genericConstrains = ParseGenericConstrains(generics, out var constrainLocations);

            // the decl (declared here because it would be used)
            var structDecl = new AstStructDecl(structName, declarations, "", null);

            ConsumeUntil(inInfo, TokenType.OpenBrace, ErrMsg("{", "at beginning of struct body"), true);

            Token next;
            SkipNewlines(inInfo);
            while (true)
            {
                next = PeekToken(inInfo);
                if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                    break;

                // clear doc string so parent's doc won't be there
                ClearDocString();
                var decl = ParseTopLevel(inInfo, ref outInfo);
                
                if (HandleStatement(decl, inInfo, ref outInfo))
                    break;
            }

            end = Consume(inInfo, TokenType.CloseBrace, ErrMsg("}", "at end of struct declaration")).Location;

            structDecl.Location = new Location(beg, end);
            structDecl.InheritedFrom = inherited;
            structDecl.HasGenericTypes = generics.Count > 0;
            structDecl.GenericConstrains = genericConstrains;
            structDecl.GenericConstrainLocations = constrainLocations;
            structDecl.IsImported = inInfo.ExternalMetadata;
            return structDecl;

            bool HandleStatement(AstStatement decl, ParserInInfo inInfo, ref ParserOutInfo outInfo)
            {
                if (decl is not AstDeclaration realDecl)
                {
                    if (decl is AstDirectiveStmt dir)
                    {
                        // cringe kostyl to handle directives
                        var statementsToAdd = HandleDirective(dir, CurrentSourceFile, inInfo, ref outInfo);
                        foreach (var s in statementsToAdd)
                        {
                            if (HandleStatement(s, inInfo, ref outInfo))
                                return true;
                        }
                    }
                    else
                        ReportMessage(decl, ["struct"], ErrorCode.Get(CTEN.DeclExpectedInClassStruct));
                    return false;
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
                if (realDecl is not AstVarDecl &&
                    realDecl is not AstFuncDecl &&
                    realDecl is not AstClassDecl &&
                    realDecl is not AstStructDecl)
                {
                    ReportMessage(realDecl, [], ErrorCode.Get(CTEN.UnexpectedDeclInStruct));
                }

                declarations.Add(realDecl);
                realDecl.ContainingParent = structDecl;

                next = PeekToken(inInfo);
                if (next.Type == TokenType.NewLine)
                {
                    SkipNewlines(inInfo);
                }
                else if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                {
                    return true;
                }
                else if (realDecl is AstVarDecl && next.Type == TokenType.Semicolon)
                {
                    // it is just a ';' at the end of class field
                    NextToken(inInfo);
                    SkipNewlines(inInfo);
                }
                return false;
            }
        }
    }
}
