using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using System.Runtime;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private Dictionary<AstIdExpr, List<AstNestedExpr>> ParseGenericConstrains(List<AstIdExpr> generics)
        {
            var inInfo = new Entities.ParserInInfo();
            var genericConstrains = new Dictionary<AstIdExpr, List<AstNestedExpr>>();

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

                List<AstNestedExpr> constrains = new List<AstNestedExpr>();
                while (!CheckTokens(TokenType.OpenBrace, TokenType.KwWhere))
                {
                    SkipNewlines();

                    AstNestedExpr ident = null;
                    var tkn = NextToken();
                    switch (tkn.Type)
                    {
                        case TokenType.Identifier:
                            {
                                ident = ParseIdentifierExpression(inInfo);
                                break;
                            }
                        case TokenType.KwStruct:
                            {
                                ident = new AstNestedExpr(new AstIdExpr("struct", tkn.Location), null, tkn.Location);
                                break;
                            }
                        case TokenType.KwClass:
                            {
                                ident = new AstNestedExpr(new AstIdExpr("class", tkn.Location), null, tkn.Location);
                                break;
                            }
                        default:
                            {
                                // error if cringe constrain
                                ReportMessage(tkn.Location, [], ErrorCode.Get(CTEN.UnexpectedGenericConstrain));
                                break;
                            }
                    }
                    constrains.Add(ident);
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
            return genericConstrains;
        }
    }
}
