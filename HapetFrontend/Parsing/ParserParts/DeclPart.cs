using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using System.Diagnostics;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private readonly List<AstAttributeStmt> _foundAttributes = new List<AstAttributeStmt>();

        private AstDeclaration ParseDeclaration(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            // keep parsing while attributes are there :)
            while (true)
            {
                SkipNewlines(inInfo);
                // special handle for destructor
                bool tildaOnTopLevel = false;
                if (CheckToken(inInfo, TokenType.Tilda))
                {
                    tildaOnTopLevel = true;
                    NextToken(inInfo);
                }

                var saved2 = inInfo.Message;
                var saved3 = inInfo.AllowMultiplyExpression;
                
                inInfo.Message = null;
                inInfo.AllowMultiplyExpression = false; // DO NOT ALLOW MULTIPLY WHEN UDECL FIRST!!!
                var expr = ParseStatement(inInfo, ref outInfo, true); // WE NEED TO PARSE ONLY ATOMIC SHITE FROM HERE :)
                inInfo.Message = saved2;
                inInfo.AllowMultiplyExpression = saved3;

                if (expr is AstDeclaration decl)
                {
                    decl.Attributes.AddRange(_foundAttributes);
                    _foundAttributes.Clear();

                    // handle dtor
                    if (decl is AstFuncDecl fnc && tildaOnTopLevel && fnc.ClassFunctionType == Enums.ClassFunctionType.Ctor)
                        fnc.ClassFunctionType = Enums.ClassFunctionType.Dtor;

                    // expect semicolon on every field decl!!!
                    if (decl is AstVarDecl && decl is not AstPropertyDecl)
                    {
                        Consume(inInfo, TokenType.Semicolon, ErrMsg(";", "at the end of the statement"));
                    }
                    return decl;
                }
                else if (expr is AstAttributeStmt attrStmt)
                {
                    _foundAttributes.Add(attrStmt);
                    // keep parsing 
                    continue;
                }
                else if (expr == null && PeekToken(inInfo).Type == TokenType.EOF)
                {
                    return null;
                }

                var a = PeekToken(inInfo);
                ReportMessage(a.Location, [], ErrorCode.Get(CTEN.ExpectedEqualOrNewline));
                return new AstVarDecl(expr as AstNestedExpr, null, null, "", expr)
                {
                    IsImported = inInfo.ExternalMetadata
                };
            }
        }
    }
}
