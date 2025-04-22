using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

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
                foreach (var g in genExpr.GenericRealTypes)
                {
                    if (g is AstNestedExpr nest)
                        generics.Add(nest.RightPart as AstIdExpr);
                    else if (g is AstIdExpr id)
                        generics.Add(id);
                    else
                        generics.Add(null); // TODO: ERROR HERE
                }
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

            ConsumeUntil(TokenType.OpenBrace, ErrMsg("{", "at beginning of struct body"), true);

            SkipNewlines();
            while (true)
            {
                var next = PeekToken();
                if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                    break;

                // get current special keys
                List<Token> specialKeys = ParseSpecialKeys();
                var decl = ParseDeclaration(inInfo, ref outInfo);

                // required!!! must not be null!!!
                ArgumentNullException.ThrowIfNull(decl);

                decl.SpecialKeys.AddRange(specialKeys);

                // it is probably an attribute so no need to save it to decls
                if (decl is AstEmptyDecl)
                {
                    SkipNewlines();
                    continue;
                }

                // error - unexpected decl in struct type
                if (decl is not AstVarDecl && decl is not AstFuncDecl)
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
                else if (decl is not AstVarDecl && decl is not AstFuncDecl)
                {
                    NextToken();
                    ReportMessage(decl.Location, [], ErrorCode.Get(CTEN.TheDeclNotAllowedInStruct));
                }
            }

            end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of struct declaration")).Location;

            // TODO: doc string
            return new AstStructDecl(structName, declarations, "", new Location(beg, end)) 
            { 
                InheritedFrom = inherited,
                HasGenericTypes = generics.Count > 0,
                GenericNames = generics,
                GenericConstrains = genericConstrains,
                IsImported = inInfo.ExternalMetadata
            };
        }
    }
}
