using HapetFrontend.Ast.Declarations;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        public AstOverloadDecl ParseOperatorOverride(UnknownDecl udecl)
        {
            string op = null;
            List<AstParamDecl> paramDecls = null;

            // cast override
            if ((CheckToken(TokenType.KwImplicit) || CheckToken(TokenType.KwExplicit)) && udecl.Type == null)
            {
                // TODO:
            }
            // this is an operator override
            else if (CheckToken(TokenType.KwOperator) && udecl.Type == null)
            {
                // skip 'operator' word
                NextToken();

                var opToken = NextToken();
                switch (opToken.Type)
                {
                    case TokenType.Equal: op = "+"; break;
                    case TokenType.Minus: op = "-"; break;
                    case TokenType.Asterisk: op = "*"; break;
                    case TokenType.ForwardSlash: op = "/"; break;
                    case TokenType.Percent: op = "%"; break;

                    case TokenType.Bang: op = "!"; break;
                    case TokenType.LogicalAnd: op = "&&"; break;
                    case TokenType.LogicalOr: op = "||"; break;

                    case TokenType.DoubleEqual: op = "=="; break;
                    case TokenType.NotEqual: op = "!="; break;
                    case TokenType.Less: op = "<"; break;
                    case TokenType.LessEqual: op = "<="; break;
                    case TokenType.Greater: op = ">"; break;
                    case TokenType.GreaterEqual: op = ">="; break;

                    case TokenType.Tilda: op = "~"; break;
                    case TokenType.Ampersand: op = "&"; break;
                    case TokenType.VerticalSlash: op = "|"; break;
                    case TokenType.Hat: op = "^"; break;
                    case TokenType.GreaterGreater: op = ">>"; break;
                    case TokenType.LessLess: op = "<<"; break;

                    case TokenType.PlusPlus: op = "++"; break;
                    case TokenType.MinusMinus: op = "--"; break;
                }

                var tpl = ParseTupleExpression(true, true);
                if (tpl is AstFuncDecl func)
                {
                    paramDecls = func.Parameters;
                }
            }
            else
            {
                // non of conditions are met
                return null;
            }

            //var overload = new AstOverloadDecl(paramDecls, );
            return null;
        }
    }
}
