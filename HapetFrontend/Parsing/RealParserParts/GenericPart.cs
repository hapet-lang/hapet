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

        private void HandleGenericWithLookAhead(AstStatement expr)
        {
            // check for generic call
            if (CheckToken(TokenType.Less) && expr is AstNestedExpr nstExpr && nstExpr.RightPart is AstIdExpr idExpr)
            {
                // loolahead cringe
                List<AstExpression> types = new List<AstExpression>();
                bool isGeneric = true;

                UpdateLookAheadLocation();
                NextLookAhead(); // eat less
                while (CheckLookAhead(TokenType.Identifier))
                {
                    // try parse type ident
                    var ident = ParseIdentifierExpression(allowGenerics: true, lookAhead: true);
                    if (ident == null || ident.RightPart == null)
                    {
                        // cringe, it is not generic shite - skip
                        isGeneric = false;
                        break;
                    }

                    types.Add(ident);

                    // eat commas
                    if (CheckLookAhead(TokenType.Comma))
                        NextLookAhead();
                }

                if (!CheckLookAhead(TokenType.Greater))
                {
                    // cringe, it is not generic shite - skip
                    isGeneric = false;
                }
                else
                    NextLookAhead(); // eat

                if (!CheckLookAhead(TokenType.OpenParen))
                {
                    // cringe, it is not generic shite - skip
                    isGeneric = false;
                }
                else
                    NextLookAhead(); // eat

                // if really generic shite
                if (isGeneric)
                {
                    types.Clear();
                    NextToken(); // eat less
                    while (CheckToken(TokenType.Identifier))
                    {
                        // try parse type ident
                        var ident = ParseIdentifierExpression(allowGenerics: true);
                        types.Add(ident);

                        // eat commas
                        if (CheckToken(TokenType.Comma))
                            NextToken();
                    }
                    Consume(TokenType.Greater, ErrMsg(">", "after generic types"));
                    if (!CheckToken(TokenType.OpenParen))
                    {
                        var custom = ErrMsg("(", "after generic types");
                        ReportMessage(PeekToken().Location, custom.MessageArgs, custom.XmlMessage);
                    }

                    // creating the generic ast id
                    nstExpr.RightPart = AstIdGenericExpr.FromAstIdExpr(idExpr, types);
                }
            }
        }
    }
}
