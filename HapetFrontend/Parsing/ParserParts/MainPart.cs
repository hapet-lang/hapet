using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        internal AstStatement ParseTopLevel(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            // get current special keys
            List<Token> specialKeys = ParseSpecialKeys();
            AstStatement toReturn = null;
            bool semicolonRequired = false;

            var tkn = PeekToken();
            switch (tkn.Type) 
            {
                case TokenType.KwClass:
                case TokenType.KwInterface:
                    toReturn = ParseClassDeclaration(inInfo, ref outInfo);
                    break;
                case TokenType.KwStruct:
                    toReturn = ParseStructDeclaration(inInfo, ref outInfo);
                    break;
                case TokenType.KwEnum:
                    toReturn = ParseEnumDeclaration(inInfo, ref outInfo);
                    break;
                case TokenType.KwDelegate:
                    toReturn = ParseDelegateDeclaration(inInfo, ref outInfo);
                    semicolonRequired = true;
                    break;
                case TokenType.SharpIdentifier:
                    toReturn = ParseDirectiveStatement();
                    break;
                case TokenType.OpenBracket:
                    toReturn = ParseAttributeStatement();
                    break;
                case TokenType.KwUsing:
                    toReturn = ParseUsingStatement();
                    semicolonRequired = true;
                    break;
                case TokenType.KwNamespace:
                    toReturn = ParseNamespaceStatement();
                    semicolonRequired = true;
                    break;
            }

            // consume semicolon after some top level statements
            if (semicolonRequired)
                Consume(TokenType.Semicolon, ErrMsg(";", "at the end of the statement"));

            // skip unneeded
            SkipNewlines();

            // add special keys
            if (toReturn is AstDeclaration decl)
                decl.SpecialKeys.AddRange(specialKeys);

            return toReturn;
        }
    }
}
