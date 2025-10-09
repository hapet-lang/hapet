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

            AstNestedExpr enumType = new AstNestedExpr(new AstIdExpr("int")
            {
                IsSyntheticStatement = true,
            }, null, null)
            {
                IsSyntheticStatement = true,
            };

            beg = Consume(inInfo, TokenType.KwEnum, ErrMsg("keyword 'enum'", "at beginning of enum type")).Location;

            // enum name
            if (!CheckToken(inInfo, TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.NoEnumNameAfterEnumWord));
                return new AstEnumDecl(new AstIdExpr("unknown"), declarations, "", beg);
            }
            else
            {
                var nest = ParseIdentifierExpression(inInfo, allowDots: false);
                if (nest.RightPart is not AstIdExpr idExpr)
                {
                    ReportMessage(nest.Location, [], ErrorCode.Get(CTEN.EnumNameNotIdent));
                    return new AstEnumDecl(new AstIdExpr("unknown"), declarations, "", beg);
                }
                enumName = idExpr;
            }
            // checking for inheritance
            if (CheckToken(inInfo, TokenType.Colon))
            {
                Consume(inInfo, TokenType.Colon, ErrMsg(":", "before inherited type"));
                SkipNewlines(inInfo);

                while (CheckToken(inInfo, TokenType.Identifier))
                {
                    var ident = ParseIdentifierExpression(inInfo);
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

            // error if there are more than 1 inherited type
            if (inherited.Count > 1)
                ReportMessage(inherited[1], [], ErrorCode.Get(CTEN.ManyInhTypesInEnum));
            else if (inherited.Count == 1)
                enumType = inherited[0];

            // creating decl here to use it further
            var enumDecl = new AstEnumDecl(enumName, declarations, "");

            ConsumeUntil(inInfo, TokenType.OpenBrace, ErrMsg("{", "at beginning of enum body"), true);

            SkipNewlines(inInfo);
            while (true)
            {
                var next = PeekToken(inInfo);
                if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                    break;

                // all enum fields are just identifiers
                if (!CheckToken(inInfo, TokenType.Identifier))
                {
                    NextToken(inInfo);
                    ReportMessage(CurrentToken.Location, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                    continue;
                }

                // getting decl parts
                AstExpression ini = null;
                AstNestedExpr type = new AstNestedExpr(enumName.GetDeepCopy() as AstIdExpr, null, enumName.Location);
                var id = ParseIdentifierExpression(inInfo, allowDots: false);
                TokenLocation fieldEnd = id.Ending;
                if (CheckToken(inInfo, TokenType.Equal))
                {
                    NextToken(inInfo);
                    var initStmt = ParseExpression(inInfo, ref outInfo);
                    if (initStmt is not AstExpression)
                        ReportMessage(initStmt.Location, [], ErrorCode.Get(CTEN.EnumFieldIniNotExpr));
                    ini = new AstCastExpr(type, initStmt as AstExpression, initStmt.Location);
                    fieldEnd = ini.Ending;
                }
                // the declaration
                // here could be a different number type!!!
                AstVarDecl decl = new AstVarDecl(type, id.RightPart as AstIdExpr, ini, "", new Location(id.Beginning, fieldEnd));

                declarations.Add(decl);
                decl.ContainingParent = enumDecl;
                // all enum fields are const
                decl.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwConst, decl.Location.Beginning));

                next = PeekToken(inInfo);
                if (next.Type == TokenType.NewLine)
                {
                    SkipNewlines(inInfo);
                }
                else if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                {
                    break;
                }
                else if (decl != null && next.Type == TokenType.Comma)
                {
                    // it is just a ',' at the end of enum field
                    NextToken(inInfo);
                    SkipNewlines(inInfo);
                }
            }

            end = Consume(inInfo, TokenType.CloseBrace, ErrMsg("}", "at end of enum declaration")).Location;

            enumDecl.Location = new Location(beg, end);
            enumDecl.InheritedType = enumType;
            enumDecl.IsImported = inInfo.ExternalMetadata;
            return enumDecl;
        }
    }
}
