using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private readonly List<AstAttributeStmt> _foundAttributesTopLevel = new List<AstAttributeStmt>();

        internal AstStatement ParseTopLevel(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            // keep parsing while attributes are there :)
            bool keepParsing = true;
            AstStatement toReturn = null;

            while (keepParsing)
            {
                // get current special keys
                List<Token> specialKeys = ParseSpecialKeys();
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
                    default:
                        toReturn = ParseDeclaration(inInfo, ref outInfo);
                        break;
                }

                // consume semicolon after some top level statements
                if (semicolonRequired)
                    Consume(TokenType.Semicolon, ErrMsg(";", "at the end of the statement"));

                // skip unneeded
                SkipNewlines();

                // we found an attr - add it to list and use it when find a decl
                if (toReturn is AstAttributeStmt attr)
                    _foundAttributesTopLevel.Add(attr);
                else
                    keepParsing = false; // stop parsing if not attr

                // add special keys
                if (toReturn is AstDeclaration decl)
                {
                    decl.SpecialKeys.AddRange(specialKeys);

                    // add previously found attributes into the declaration
                    decl.Attributes.AddRange(_foundAttributesTopLevel);
                    _foundAttributesTopLevel.Clear();
                }
            }

            return toReturn;
        }
    }
}
