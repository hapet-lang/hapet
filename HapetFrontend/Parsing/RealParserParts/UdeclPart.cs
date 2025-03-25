using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Errors;
using System.Collections.Generic;
using System.Runtime;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstDeclaration PrepareUnknownDecl(AstUnknownDecl udecl, List<AstAttributeStmt> attrs)
        {
            TokenLocation end = udecl.Ending;
            AstStatement initializer = null;
            var savedUdecl = inInfo.CurrentUdecl;
            inInfo.CurrentUdecl = udecl;

            // disable new as sk allowance!!!
            inInfo.AllowNewAsSpecialKey = false;

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
                OnExit();
                return varDecl;
            }
            // func declaration 
            else if (CheckToken(TokenType.OpenParen))
            {
                var saved1 = inInfo.AllowFunctionDeclaration;
                var saved2 = inInfo.AllowCommaForTuple;
                inInfo.AllowFunctionDeclaration = true;
                inInfo.AllowCommaForTuple = true;
                var tpl = ParseTupleExpression(inInfo, ref outInfo);
                inInfo.AllowFunctionDeclaration = saved1;
                inInfo.AllowCommaForTuple = saved2;

                if (tpl is AstFuncDecl func)
                {
                    if (udecl.Type == null)
                    {
                        // it is ctor/dtor
                        // func.Name = udecl.Name.GetCopy(udecl.Name.Name + (udecl.Name.Suffix != "~" ? "_ctor" : "_dtor")); // no need anymore?
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
                // TODO: could there be a lambda???
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

            // possible operator override
            var result = ParseOperatorOverride(udecl);
            if (result != null)
            {
                result.Attributes.AddRange(attrs);
                OnExit();
                return result;
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
