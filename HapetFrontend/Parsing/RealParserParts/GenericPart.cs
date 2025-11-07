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
        private Dictionary<AstIdExpr, List<AstConstrainStmt>> ParseGenericConstrains(List<AstIdExpr> generics, out List<(ILocation, ILocation)> locations)
        {
            var inInfo = new Entities.ParserInInfo();
            var genericConstrains = new Dictionary<AstIdExpr, List<AstConstrainStmt>>();
            locations = new List<(ILocation, ILocation)>();

            var tst = PeekToken(inInfo);

            // checking for generic constrains
            // https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters
            while (CheckToken(inInfo, TokenType.KwWhere))
            {
                var whereTkn = Consume(inInfo, TokenType.KwWhere, ErrMsg("where", "before generic constrains"));

                // generic type name has to be here
                if (!CheckToken(inInfo, TokenType.Identifier))
                {
                    ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.GenericTypeNameExpected));
                    continue;
                }
                // has to be identifier (nested is also not allowed!!!)
                var typeNameExpr = ParseIdentifierExpression(inInfo);
                if (typeNameExpr.RightPart is not AstIdExpr nameIdentExpr)
                {
                    ReportMessage(typeNameExpr, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                    continue;
                }

                // add it to locations
                locations.Add((whereTkn.Location, typeNameExpr.Location));

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
                var tmp = Consume(inInfo, TokenType.Colon, ErrMsg(":", "before generic constrain types"));
                if (tmp == null)
                    continue;

                SkipNewlines(inInfo);

                List<AstConstrainStmt> constrains = new List<AstConstrainStmt>();
                while (!CheckTokens(inInfo, TokenType.OpenBrace, TokenType.KwWhere))
                {
                    SkipNewlines(inInfo);

                    AstNestedExpr ident = null;
                    List<AstNestedExpr> additionalExprs = new List<AstNestedExpr>();
                    GenericConstrainType constrainType = GenericConstrainType.None;

                    var tkn = PeekToken(inInfo);
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
                                NextToken(inInfo);
                                ident = new AstNestedExpr(new AstIdExpr("struct", tkn.Location), null, tkn.Location);
                                constrainType = GenericConstrainType.StructType;
                                break;
                            }
                        case TokenType.KwClass:
                            {
                                NextToken(inInfo);
                                ident = new AstNestedExpr(new AstIdExpr("class", tkn.Location), null, tkn.Location);
                                constrainType = GenericConstrainType.ClassType;
                                break;
                            }
                        case TokenType.KwDelegate:
                            {
                                NextToken(inInfo);
                                ident = new AstNestedExpr(new AstIdExpr("delegate", tkn.Location), null, tkn.Location);
                                constrainType = GenericConstrainType.DelegateType;
                                break;
                            }
                        case TokenType.KwEnum:
                            {
                                NextToken(inInfo);
                                ident = new AstNestedExpr(new AstIdExpr("enum", tkn.Location), null, tkn.Location);
                                constrainType = GenericConstrainType.EnumType;
                                break;
                            }
                        case TokenType.KwNew:
                            {
                                NextToken(inInfo);
                                ident = new AstNestedExpr(new AstIdExpr("new", tkn.Location), null, tkn.Location);
                                constrainType = GenericConstrainType.NewType;

                                Consume(inInfo, TokenType.OpenParen, ErrMsg("(", "after 'new' keyword"));
                                while (CheckToken(inInfo, TokenType.Identifier))
                                {
                                    var id = ParseIdentifierExpression(inInfo);
                                    additionalExprs.Add(id);

                                    if (CheckToken(inInfo, TokenType.Comma))
                                        NextToken(inInfo);
                                    else if (CheckToken(inInfo, TokenType.CloseParen))
                                        break;
                                }
                                Consume(inInfo, TokenType.CloseParen, ErrMsg(")", "after 'new' keyword"));
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
                    if (CheckToken(inInfo, TokenType.Comma))
                    {
                        Consume(inInfo, TokenType.Comma, ErrMsg(",", "before the next constrain"));
                        continue;
                    }

                    // if there is nothing else
                    break;
                }

                // add it
                genericConstrains.Add(nameIdentExpr, constrains);

                SkipNewlines(inInfo);
            }

            // we need to create an empty list of constrains if there are no user-defined
            foreach (var g in generics)
            {
                // add if does not exist
                if (!genericConstrains.ContainsKey(g))
                    genericConstrains.Add(g, new List<AstConstrainStmt>());
            }

            return genericConstrains;
        }
    }
}
