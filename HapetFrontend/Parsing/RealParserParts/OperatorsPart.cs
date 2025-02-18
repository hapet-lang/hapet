using HapetFrontend.Ast.Declarations;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        public AstOverloadDecl ParseOperatorOverride(UnknownDecl udecl)
        {
            // cast override
            if ((CheckToken(TokenType.KwImplicit) || CheckToken(TokenType.KwExplicit)) && udecl.Type == null)
            {
                // TODO:
            }
            // this is an operator override
            else if (CheckToken(TokenType.KwOperator) && udecl.Type == null)
            {
                var opToken = NextToken();
                string op = null;
                switch (opToken.Type)
                {
                    case TokenType.AddEq: op = "+"; break;
                    case TokenType.SubEq: op = "-"; break;
                    case TokenType.MulEq: op = "*"; break;
                    case TokenType.DivEq: op = "/"; break;
                    case TokenType.ModEq: op = "%"; break;
                }


            }

            return null;
        }
    }
}
