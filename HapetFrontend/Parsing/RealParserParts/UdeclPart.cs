using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Errors;
using System.Collections.Generic;
using System.Runtime;
using HapetFrontend.Entities;
using HapetFrontend.Extensions;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstDeclaration PrepareUnknownDecl(AstUnknownDecl udecl, List<AstAttributeStmt> attrs, ParserInInfo inInfo, ref ParserOutInfo outInfo, ref bool semicolonExpected)
        {
            TokenLocation end = udecl.Ending;
            AstStatement initializer = null;
            var savedUdecl = inInfo.CurrentUdecl;
            inInfo.CurrentUdecl = udecl;

            // variable declaration with initializer
            if (CheckToken(TokenType.Equal))
            {
                NextToken();
                initializer = ParseExpression(inInfo, ref outInfo);
                end = initializer.Ending;

                if (initializer is not AstExpression)
                {
                    ReportMessage(initializer.Location, [], ErrorCode.Get(CTEN.VarIniterExpr));
                }

                var varDecl = new AstVarDecl(udecl.Type, udecl.Name, initializer as AstExpression, udecl.Documentation, new Location(udecl.Beginning, end));
                varDecl.Attributes.AddRange(attrs);
                varDecl.SpecialKeys.AddRange(udecl.SpecialKeys);
                varDecl.IsImported = inInfo.ExternalMetadata;

                semicolonExpected = true;
                OnExit();
                return varDecl;
            }
            // variable declaration without initializer
            else if (CheckToken(TokenType.Semicolon))
            {
                // do not get the next token
                var varDecl = new AstVarDecl(udecl.Type, udecl.Name, null, udecl.Documentation, new Location(udecl.Beginning, end));
                varDecl.Attributes.AddRange(attrs);
                varDecl.SpecialKeys.AddRange(udecl.SpecialKeys);
                varDecl.IsImported = inInfo.ExternalMetadata;

                semicolonExpected = true;
                OnExit();
                return varDecl;
            }
            // func declaration 
            else if (CheckToken(TokenType.OpenParen))
            {
                var func = ParseFuncDeclaration(null, null, inInfo, ref outInfo);
                if (udecl.Type == null)
                {
                    // it is ctor/dtor
                    func.Name = udecl.Name.GetCopy();
                    func.Returns = new AstNestedExpr(new AstIdExpr("void"), null);
                    // check that it is a static ctor
                    if (udecl.Name.Suffix != "~" && udecl.SpecialKeys.Contains(TokenType.KwStatic))
                        func.ClassFunctionType = Enums.ClassFunctionType.StaticCtor;
                    else
                        func.ClassFunctionType = udecl.Name.Suffix != "~" ? Enums.ClassFunctionType.Ctor : Enums.ClassFunctionType.Dtor;
                }
                else
                {
                    // it is normal func
                    func.Name = udecl.Name;
                    func.Returns = udecl.Type;
                }
                func.Attributes.AddRange(attrs);
                func.SpecialKeys.AddRange(udecl.SpecialKeys);
                OnExit();
                return func;
            }
            // properties 
            else if (CheckToken(TokenType.OpenBrace))
            {
                var prop = PreparePropertyDecl(udecl, udecl.Documentation);
                prop.Attributes.AddRange(attrs);
                // special keys are added inside PreparePropertyDecl
                OnExit();
                return prop;
            }
            // indexer?
            else if (CheckToken(TokenType.OpenBracket) && udecl.Name.Name == "this")
            {
                Consume(TokenType.OpenBracket, ErrMsg("symbol '['", "at beginning of indexer param declaration"));
                var par = ParseParameter(false); // no default value for indexer
                Consume(TokenType.OpenBracket, ErrMsg("symbol ']'", "at ending of indexer param declaration"));

                SkipNewlines();
                // TODO: doc 
                udecl.Name = udecl.Name.GetCopy("indexer__");
                var prop = PreparePropertyDecl(udecl, "") as AstPropertyDecl;
                var indexer = new AstIndexerDecl(prop);
                indexer.IndexerParameter = par;

                return indexer;
            }
            // operator overloads
            else if (CheckToken(TokenType.KwOperator))
            {
                // possible operator override
                var result = ParseOperatorOverride(udecl);
                if (result != null)
                {
                    result.Attributes.AddRange(attrs);
                    OnExit();
                    return result;
                }
            }

            ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.PureUnexpectedToken)); // TODO: better error message?
            OnExit();
            return udecl;

            void OnExit()
            {
                inInfo.CurrentUdecl = savedUdecl;
            }
        }
    }
}
