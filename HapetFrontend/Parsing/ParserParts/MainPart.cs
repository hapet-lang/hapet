using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
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

            // expect newline
            var next = PeekToken();
            if (inInfo.ExpectNewline && next.Type != TokenType.NewLine && next.Type != TokenType.EOF)
            {
                ReportMessage(next.Location, [], ErrorCode.Get(CTEN.NewlineExpected));
                RecoverStatement();
            }

            return toReturn;
        }
    }
}
