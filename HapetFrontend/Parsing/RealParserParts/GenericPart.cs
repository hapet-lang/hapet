using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private Dictionary<AstIdExpr, List<AstNestedExpr>> ParseGenericConstrains(List<AstIdExpr> generics)
        {
            var genericConstrains = new Dictionary<AstIdExpr, List<AstNestedExpr>>();

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
                var typeNameExpr = ParseIdentifierExpression();
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

                List<AstNestedExpr> constrains = new List<AstNestedExpr>();
                while (CheckToken(TokenType.Identifier))
                {
                    var ident = ParseIdentifierExpression();
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
