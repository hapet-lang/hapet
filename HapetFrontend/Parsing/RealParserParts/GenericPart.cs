using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using System.Collections.Generic;
using System.Runtime;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private Dictionary<AstIdExpr, List<AstConstrainStmt>> ParseGenericConstrains(List<AstIdExpr> generics)
        {
            var inInfo = new Entities.ParserInInfo();
            var genericConstrains = new Dictionary<AstIdExpr, List<AstConstrainStmt>>();

            var tst = PeekToken();

            // checking for generic constrains
            // https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters
            while (CheckToken(TokenType.KwWhere))
            {
                Consume(TokenType.KwWhere, ErrMsg("where", "before generic constrains"));

                // generic type name has to be here
                if (!CheckToken(TokenType.Identifier))
                {
                    ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.GenericTypeNameExpected));
                    continue;
                }
                // has to be identifier (nested is also not allowed!!!)
                var typeNameExpr = ParseIdentifierExpression(inInfo);
                if (typeNameExpr.RightPart is not AstIdExpr nameIdentExpr)
                {
                    ReportMessage(typeNameExpr, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                    continue;
                }
                // check if the generic type even exists
                var nameTmp = generics.FirstOrDefault(x => x.Name == nameIdentExpr.Name);
                if (nameTmp == null)
                {
                    ReportMessage(typeNameExpr, [], ErrorCode.Get(CTEN.GenericTypeNotFound));
                    continue;
                }
                // for dictionary
                nameIdentExpr = nameTmp;

                // get the colon before constain types
                var tmp = Consume(TokenType.Colon, ErrMsg(":", "before generic constrain types"));
                if (tmp == null)
                    continue;

                SkipNewlines();

                List<AstConstrainStmt> constrains = new List<AstConstrainStmt>();
                while (!CheckTokens(TokenType.OpenBrace, TokenType.KwWhere))
                {
                    SkipNewlines();

                    AstNestedExpr ident = null;
                    List<AstNestedExpr> additionalExprs = new List<AstNestedExpr>();
                    GenericConstrainType constrainType = GenericConstrainType.None;

                    var tkn = NextToken();
                    switch (tkn.Type)
                    {
                        case TokenType.Identifier:
                            {
                                ident = ParseIdentifierExpression(inInfo);
                                constrainType = GenericConstrainType.CustomType;
                                break;
                            }
                        case TokenType.KwStruct:
                            {
                                ident = new AstNestedExpr(new AstIdExpr("struct", tkn.Location), null, tkn.Location);
                                constrainType = GenericConstrainType.StructType;
                                break;
                            }
                        case TokenType.KwClass:
                            {
                                ident = new AstNestedExpr(new AstIdExpr("class", tkn.Location), null, tkn.Location);
                                constrainType = GenericConstrainType.ClassType;
                                break;
                            }
                        case TokenType.KwDelegate:
                            {
                                ident = new AstNestedExpr(new AstIdExpr("delegate", tkn.Location), null, tkn.Location);
                                constrainType = GenericConstrainType.DelegateType;
                                break;
                            }
                        case TokenType.KwEnum:
                            {
                                ident = new AstNestedExpr(new AstIdExpr("enum", tkn.Location), null, tkn.Location);
                                constrainType = GenericConstrainType.EnumType;
                                break;
                            }
                        case TokenType.KwNew:
                            {
                                ident = new AstNestedExpr(new AstIdExpr("new", tkn.Location), null, tkn.Location);
                                constrainType = GenericConstrainType.NewType;

                                Consume(TokenType.OpenParen, ErrMsg("(", "after 'new' keyword"));
                                while (CheckToken(TokenType.Identifier))
                                {
                                    var id = ParseIdentifierExpression(inInfo);
                                    additionalExprs.Add(id);

                                    if (CheckToken(TokenType.Comma))
                                        NextToken();
                                    else if (CheckToken(TokenType.CloseParen))
                                        break;
                                }
                                Consume(TokenType.CloseParen, ErrMsg(")", "after 'new' keyword"));
                                break;
                            }
                        default:
                            {
                                // error if cringe constrain
                                ReportMessage(tkn.Location, [], ErrorCode.Get(CTEN.UnexpectedGenericConstrain));
                                break;
                            }
                    }

                    // possible null
                    if (ident != null)
                    {
                        constrains.Add(new AstConstrainStmt(ident, constrainType, ident.Location)
                        {
                            AdditionalExprs = additionalExprs,
                        });
                    }
                    
                    // if there is something else
                    if (CheckToken(TokenType.Comma))
                    {
                        Consume(TokenType.Comma, ErrMsg(",", "before the next constrain"));
                        continue;
                    }

                    // if there is nothing else
                    break;
                }

                // add it
                genericConstrains.Add(nameIdentExpr, constrains);

                SkipNewlines();
            }

            // we need to manually add 'object' constrain
            foreach (var g in generics)
            {
                // add if does not exist
                if (!genericConstrains.ContainsKey(g))
                    genericConstrains.Add(g, new List<AstConstrainStmt>());

                // manually adding the constrain
                genericConstrains[g].Add(new AstConstrainStmt(
                    new AstNestedExpr(new AstIdExpr("System.Object", g.Location), null, g.Location),
                    GenericConstrainType.CustomType,
                    g.Location)
                {
                    AdditionalExprs = new List<AstNestedExpr>(),
                });
            }

            return genericConstrains;
        }
    }
}
