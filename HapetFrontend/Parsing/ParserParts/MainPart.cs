using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using System;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        internal AstStatement ParseTopLevel(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            // keep parsing while attributes are there :)
            bool keepParsing = true;
            AstStatement toReturn = null;

            // current attributes handler
            List<AstAttributeStmt> foundAttributesTopLevel = new List<AstAttributeStmt>();

            while (keepParsing)
            {
                // skip unneeded
                SkipNewlines();

                // getting doc string
                var docString = GetCurrentDocString();

                // skip unneeded
                SkipNewlines();

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
                        toReturn = ParseDirectiveStatement(inInfo, ref outInfo);
                        break;
                    case TokenType.OpenBracket:
                        toReturn = ParseAttributeStatement(inInfo, ref outInfo);
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
                    foundAttributesTopLevel.Add(attr);
                else
                    keepParsing = false; // stop parsing if not attr

                // add special keys
                if (toReturn is AstDeclaration decl)
                {
                    // saving doc string
                    ClearDocString();
                    decl.Documentation = docString;

                    // append special keys
                    decl.SpecialKeys.AddRange(specialKeys);

                    // add previously found attributes into the declaration
                    decl.Attributes.AddRange(foundAttributesTopLevel);
                    foundAttributesTopLevel.Clear();

                    // handle for stor/ctor
                    if (decl is AstFuncDecl fnc && fnc.ClassFunctionType == Enums.ClassFunctionType.Special)
                    {
                        // check that it is a static ctor
                        if (fnc.SpecialKeys.Contains(TokenType.KwStatic)) 
                            fnc.ClassFunctionType = Enums.ClassFunctionType.StaticCtor;
                        else 
                            fnc.ClassFunctionType = Enums.ClassFunctionType.Ctor;
                    }
                }
            }

            return toReturn;
        }
    }
}
