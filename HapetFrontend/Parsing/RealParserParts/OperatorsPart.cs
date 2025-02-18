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
                    case TokenType.Equal: op = "+"; break;
                    case TokenType.Minus: op = "-"; break;
                    case TokenType.Asterisk: op = "*"; break;
                    case TokenType.ForwardSlash: op = "/"; break;
                    case TokenType.Percent: op = "%"; break;
                    case TokenType.Bang: op = "!"; break;
                    case TokenType.Tilda: op = "~"; break;
                }


            }

            return null;
        }
    }
}
