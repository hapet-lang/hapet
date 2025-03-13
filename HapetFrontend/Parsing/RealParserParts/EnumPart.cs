using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstDeclaration ParseEnumDeclaration(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            TokenLocation beg = null, end = null;
            var declarations = new List<AstVarDecl>();
            var inherited = new List<AstNestedExpr>();
            AstIdExpr enumName = null;

            AstNestedExpr enumType = new AstNestedExpr(new AstIdExpr("int"), null, null);

            beg = Consume(TokenType.KwEnum, ErrMsg("keyword 'enum'", "at beginning of enum type")).Location;

            // enum name
            if (!CheckToken(TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.NoEnumNameAfterEnumWord));
            }
            else
            {
                var nest = ParseIdentifierExpression(allowDots: false);
                if (nest.RightPart is not AstIdExpr idExpr)
                {
                    ReportMessage(nest.Location, [], ErrorCode.Get(CTEN.EnumNameNotIdent));
                    return new AstEnumDecl(new AstIdExpr("unknown"), declarations, "", beg);
                }
                enumName = idExpr;
            }
            // checking for inheritance
            if (CheckToken(TokenType.Colon))
            {
                Consume(TokenType.Colon, ErrMsg(":", "before inherited type"));
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

            // error if there are more than 1 inherited type
            if (inherited.Count > 1)
                ReportMessage(inherited[1], [], ErrorCode.Get(CTEN.ManyInhTypesInEnum));
            else if (inherited.Count == 1)
                enumType = inherited[0];

            ConsumeUntil(TokenType.OpenBrace, ErrMsg("{", "at beginning of enum body"), true);

            SkipNewlines();
            while (true)
            {
                var next = PeekToken();
                if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                    break;

                // all enum fields are just identifiers
                if (!CheckToken(TokenType.Identifier))
                {
                    NextToken();
                    ReportMessage(CurrentToken.Location, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                    continue;
                }

                // getting decl parts
                AstExpression ini = null;
                var id = ParseIdentifierExpression(allowDots: false);
                TokenLocation fieldEnd = id.Ending;
                if (CheckToken(TokenType.Equal))
                {
                    NextToken();
                    var initStmt = ParseExpression(inInfo, ref outInfo);
                    if (initStmt is not AstExpression)
                        ReportMessage(initStmt.Location, [], ErrorCode.Get(CTEN.EnumFieldIniNotExpr));
                    ini = initStmt as AstExpression;
                    fieldEnd = ini.Ending;
                }
                // the declaration
                // here could be a different number type!!!
                AstVarDecl decl = new AstVarDecl(enumType, id.RightPart as AstIdExpr, ini, "", new Location(id.Beginning, fieldEnd));

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
                else if (decl is AstVarDecl && next.Type == TokenType.Comma)
                {
                    // it is just a ',' at the end of enum field
                    NextToken();
                    SkipNewlines();
                }
                else if (decl is not AstVarDecl)
                {
                    NextToken();
                    ReportMessage(decl.Location, [], ErrorCode.Get(CTEN.ThisDeclNotAllowedInEnum));
                }
            }

            end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of enum declaration")).Location;

            // TODO: doc string
            var enm = new AstEnumDecl(enumName, declarations, "", new Location(beg, end))
            {
                InheritedType = enumType,
                IsImported = inInfo.ExternalMetadata
            };
            return enm;
        }
    }
}
