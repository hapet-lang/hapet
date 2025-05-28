namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private List<Token> ParseSpecialKeys()
        {
            bool running = true;
            List<Token> keys = new List<Token>();
            while (running)
            {
                var tkn = PeekToken();
                switch (tkn.Type) 
                {
                    // custom shite
                    case TokenType.KwPublic:
                    case TokenType.KwInternal:
                    case TokenType.KwProtected:
                    case TokenType.KwPrivate:
                    case TokenType.KwUnreflected:

                    case TokenType.KwAsync:

                    case TokenType.KwNew:

                    case TokenType.KwConst:
                    case TokenType.KwReadonly:

                    case TokenType.KwStatic:

                    case TokenType.KwAbstract:
                    case TokenType.KwVirtual:
                    case TokenType.KwOverride:
                    case TokenType.KwPartial:
                    case TokenType.KwExtern:
                    case TokenType.KwInline:
                    case TokenType.KwSealed:
                    case TokenType.KwUnsafe:
                        keys.Add(tkn);
                        NextToken();
                        break;
                    default:
                        running = false;
                        break;
                }
            }
            return keys;
        }
    }
}
