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
        private AstDeclaration ParseClassDeclaration(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            TokenLocation beg = null, end = null;
            var declarations = new List<AstDeclaration>();
            var inherited = new List<AstNestedExpr>();
            var generics = new List<AstIdExpr>();
            AstIdExpr className = null;
            bool isInterface = false;

            // check if it is an interface decl
            if (CheckToken(inInfo, TokenType.KwClass))
            {
                beg = Consume(inInfo, TokenType.KwClass, ErrMsg("keyword 'class'", "at beginning of class type")).Location;
            }
            else
            {
                beg = Consume(inInfo, TokenType.KwInterface, ErrMsg("keyword 'interface'", "at beginning of interface type")).Location;
                isInterface = true;
            }

            // class name
            if (!CheckToken(inInfo, TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.ClassNameExpected));
            }
            else
            {
                var nest = ParseIdentifierExpression(inInfo, allowDots: false, allowGenerics: true);
                if (nest.RightPart is not AstIdExpr idExpr)
                {
                    ReportMessage(nest.Location, [], ErrorCode.Get(CTEN.ClassNameNotIdent));
                    return new AstClassDecl(new AstIdExpr("unknown"), declarations, "", beg) { IsInterface = isInterface };
                }
                className = idExpr;
            }

            // checking generics
            // getting generics from parsed class name
            if (className is AstIdGenericExpr genExpr)
            {
                generics = GenericsHelper.GetGenericsFromName(genExpr, _messageHandler);
            }

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
            var classDecl = new AstClassDecl(className, declarations, "", null);

            ConsumeUntil(inInfo, TokenType.OpenBrace, ErrMsg("{", "at beginning of class body"), true);

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
            end = Consume(inInfo, TokenType.CloseBrace, ErrMsg("}", "at end of declaration")).Location;

            if (isInterface)
            {
                // check if there are fields - warn user
                var field = declarations.FirstOrDefault(x => x is AstVarDecl && x is not AstPropertyDecl);
                if (field != null)
                    ReportMessage(field.Location, [], ErrorCode.Get(CTWN.FieldsInInterface), reportType: Entities.ReportType.Warning);
            }

            classDecl.Location = new Location(beg, end);
            classDecl.InheritedFrom = inherited;
            classDecl.IsInterface = isInterface;
            classDecl.HasGenericTypes = generics.Count > 0;
            classDecl.GenericConstrains = genericConstrains;
            classDecl.GenericConstrainLocations = constrainLocations;
            classDecl.IsImported = inInfo.ExternalMetadata;
            return classDecl;

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
                        ReportMessage(decl, ["class"], ErrorCode.Get(CTEN.DeclExpectedInClassStruct));
                    return false;
                }

                // mark the decl as nested if it is:
                if (realDecl is AstClassDecl ||
                    realDecl is AstStructDecl ||
                    realDecl is AstEnumDecl ||
                    realDecl is AstDelegateDecl)
                {
                    realDecl.IsNestedDecl = true;
                    realDecl.ParentDecl = classDecl;
                }
                declarations.Add(realDecl);
                realDecl.ContainingParent = classDecl;

                next = PeekToken(inInfo);
                if (next.Type == TokenType.NewLine)
                {
                    SkipNewlines(inInfo);
                }
                else if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                {
                    return true;
                }
                else if (decl is AstVarDecl && next.Type == TokenType.Semicolon)
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
