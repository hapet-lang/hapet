using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        public static AstExpression ParseType(string tp, Compiler compiler)
        {
            var lexer = Lexer.FromString(tp, compiler.MessageHandler);
            var parser = new Parser(lexer, compiler.MessageHandler);

            AstExpression id = parser.ParseIdentifierExpression();

            // if it is a pointer or array type
            while (parser.CheckToken(TokenType.Asterisk) || parser.CheckToken(TokenType.ArrayDef))
            {
                if (parser.CheckToken(TokenType.ArrayDef))
                {
                    var arrExpr = new AstArrayExpr(id, new Location(id.Beginning, parser.CurrentToken.Location.Ending));
                    id = arrExpr;
                }
                else
                {
                    var ptrExpr = new AstPointerExpr(id, false, new Location(id.Beginning, parser.CurrentToken.Location.Ending));
                    id = ptrExpr;
                }
                parser.NextToken();
            }
            return id;
        }
    }
}
