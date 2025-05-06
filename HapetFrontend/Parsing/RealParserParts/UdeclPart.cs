using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Errors;
using System.Collections.Generic;
using System.Runtime;
using HapetFrontend.Entities;
using HapetFrontend.Extensions;
using HapetFrontend.Types;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement PrepareUnknownDecl(AstUnknownDecl udecl, List<AstAttributeStmt> attrs, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            TokenLocation end = udecl.Ending;
            AstStatement initializer = null;
            var savedUdecl = inInfo.CurrentUdecl;
            inInfo.CurrentUdecl = udecl;

            // variable declaration with initializer
            // allow shite like (int a, int b) = ...
            if (CheckToken(TokenType.Equal) && (udecl.Name != null || (udecl.Type is AstTupleExpr tpl && tpl.IsFullyNamed)))
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
            // assing to var
            else if (CheckTokens(TokenType.Equal, TokenType.AddEq, TokenType.SubEq, TokenType.MulEq, TokenType.DivEq, TokenType.ModEq, TokenType.CoalesceEq) && udecl.Name == null)
            {
                var currT = NextToken();
                var x = currT.Type;
                string op = null;
                switch (x)
                {
                    case TokenType.AddEq: op = "+"; break;
                    case TokenType.SubEq: op = "-"; break;
                    case TokenType.MulEq: op = "*"; break;
                    case TokenType.DivEq: op = "/"; break;
                    case TokenType.ModEq: op = "%"; break;
                    case TokenType.CoalesceEq: op = "??"; break;
                }
                SkipNewlines();

                var val = ParseExpression(inInfo, ref outInfo);

                if (val is not AstExpression valExpr)
                {
                    ReportMessage(val.Location, [], ErrorCode.Get(CTEN.RightSideVarDeclNotExpr));
                    OnExit();
                    return udecl;
                }

                // just needed :)
                if (udecl.Type is AstIdExpr idExpr)
                    udecl.Type = new AstNestedExpr(idExpr, null, idExpr);

                if (udecl.Type is AstNestedExpr id && currT.Type != TokenType.Equal)
                {
                    // expand ops like 'a += b' into 'a = a + b'
                    AstExpression binOpExpr = new AstBinaryExpr(op, id, valExpr, new Location(id.Location.Beginning, val.Location.Ending));
                    if (op == "??")
                    {
                        /// WARN!!!: the same as in <see cref="ParseNullCoalescingExpression"/>
                        // creating null comparison
                        var nulll = new AstNullExpr(PointerType.NullLiteralType, id);
                        var nullComparison = new AstBinaryExpr("==", id.GetDeepCopy() as AstExpression, nulll, id);
                        var ternOp = new AstTernaryExpr(nullComparison, valExpr, id.GetDeepCopy() as AstExpression, binOpExpr.Location);
                        binOpExpr = ternOp;
                    }
                    var toReturn = new AstAssignStmt(id, binOpExpr, new Location(udecl.Type.Beginning, val.Ending));
                    OnExit();
                    return toReturn;
                }
                else if (udecl.Type is AstNestedExpr nestId && currT.Type == TokenType.Equal)
                {
                    var toReturn = new AstAssignStmt(nestId, valExpr, new Location(udecl.Type.Beginning, val.Ending));
                    OnExit();
                    return toReturn;
                }
                else
                {
                    // error here probably
                    ReportMessage(udecl.Type.Location, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                }
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
                var func = ParseFuncDeclaration(null, null, inInfo, ref outInfo);
                if (udecl.Name == null)
                {
                    var fncName = udecl.Type is AstIdExpr ? (udecl.Type as AstIdExpr) : ((udecl.Type as AstNestedExpr).RightPart as AstIdExpr);

                    // it is ctor/dtor
                    func.Name = fncName.GetCopy();
                    func.Returns = new AstNestedExpr(new AstIdExpr("void"), null);
                    // check that it is a static ctor
                    if (fncName.Suffix != "~" && udecl.SpecialKeys.Contains(TokenType.KwStatic))
                        func.ClassFunctionType = Enums.ClassFunctionType.StaticCtor;
                    else
                        func.ClassFunctionType = fncName.Suffix != "~" ? Enums.ClassFunctionType.Ctor : Enums.ClassFunctionType.Dtor;
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
                var a = PeekToken();
                Consume(TokenType.CloseBracket, ErrMsg("symbol ']'", "at ending of indexer param declaration"));

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

            ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.PureUnexpectedToken)); // better error message?
            OnExit();
            return udecl;

            void OnExit()
            {
                inInfo.CurrentUdecl = savedUdecl;
            }
        }
    }
}
