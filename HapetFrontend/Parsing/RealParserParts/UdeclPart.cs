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
        private int _numberOfCurrentTmpTupleHandler = 0;
        private AstStatement PrepareUnknownDecl(AstUnknownDecl udecl, List<AstAttributeStmt> attrs, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            TokenLocation end = udecl.Ending;
            AstStatement initializer = null;
            var savedUdecl = inInfo.CurrentUdecl;
            inInfo.CurrentUdecl = udecl;

            // variable declaration with initializer
            // allow shite like (int a, int b) = ...
            bool isFullyNamedTuple = (udecl.Type is AstNestedExpr nst && nst.RightPart is AstTupleExpr tpl && tpl.IsFullyNamed);

            // this is possible when function return type is fully named tuple
            // like: public static (int a, int b) SomeFunc() ...
            if (isFullyNamedTuple && CheckToken(inInfo, TokenType.Identifier) && udecl.Name == null)
            {
                /// WARN: same as in <see cref="ParseAtomicExpression"/>
                // allowDots is true because of explicit interface impls
                // allowGenerics because of Anime<T>.Func explicit impls
                var name = ParseIdentifierExpression(inInfo, allowDots: true, allowGenerics: true, expectIdent: true, allowTupled: (inInfo.AllowTypedTuple && !inInfo.IsInTupleParsing));
                if (name.RightPart is not AstIdExpr idExpr)
                {
                    ReportMessage(name.Location, [], ErrorCode.Get(CTEN.DeclNameIsNotIdent));
                }
                udecl.Name = name.RightPart as AstIdExpr;
            }

            if (CheckToken(inInfo, TokenType.Equal) && (udecl.Name != null || isFullyNamedTuple))
            {
                NextToken(inInfo);
                initializer = ParseExpression(inInfo, ref outInfo);
                end = initializer.Ending;

                if (initializer is not AstExpression)
                {
                    ReportMessage(initializer.Location, [], ErrorCode.Get(CTEN.VarIniterExpr));
                }

                // name is empty when fully named tuple
                if (udecl.Name == null && isFullyNamedTuple)
                {
                    // this code will do this shite:
                    // (int a, string b) = SomeCringeFunc();
                    // is going to be:
                    // (int, string) tmp = SomeCringeFunc();
                    // int a = tmp.Item1;
                    // string b = tmp.Item2;

                    var tuple = (udecl.Type as AstNestedExpr).RightPart as AstTupleExpr;
                    // we need to name a tmp var:
                    udecl.Name = new AstIdExpr($"dev:_tmpTuple{_numberOfCurrentTmpTupleHandler++}", tuple.Location); // TODO: different names for tmps
                    for (int i = 0; i < tuple.Names.Count; ++i)
                    {
                        var type = tuple.Elements[i].GetDeepCopy() as AstExpression;
                        var name = tuple.Names[i];
                        var init = new AstNestedExpr(new AstIdExpr($"Item{i + 1}", tuple.Location), new AstNestedExpr(udecl.Name.GetCopy(), null, tuple.Location));
                        var varDeclInside = new AstVarDecl(type, name, init, "", name.Location);
                        outInfo.StatementsToAddAfter.Add(varDeclInside);
                    }
                    // reset names, no need for them anymore
                    tuple.Names = null;
                }

                // if there are multiple names
                if (udecl.Name is AstIdTupledExpr tupledName)
                {
                    // this code will do this shite:
                    // (int, string) a, b = SomeCringeFunc();
                    // is going to be:
                    // (int, string) tmp = SomeCringeFunc();
                    // int a = tmp.Item1;
                    // string b = tmp.Item2;

                    var tuple = (udecl.Type as AstNestedExpr).RightPart as AstTupleExpr;
                    // we need to name a tmp var:
                    udecl.Name = new AstIdExpr($"dev:_tmpTuple{_numberOfCurrentTmpTupleHandler++}", tuple.Location); // TODO: different names for tmps
                    for (int i = 0; i < tupledName.RealNames.Count; ++i)
                    {
                        var type = tuple.Elements[i].GetDeepCopy() as AstExpression;
                        var name = tupledName.RealNames[i];
                        var init = new AstNestedExpr(new AstIdExpr($"Item{i + 1}", tuple.Location), new AstNestedExpr(udecl.Name.GetCopy(), null, tuple.Location));
                        var varDeclInside = new AstVarDecl(type, name, init, "", name.Location);
                        outInfo.StatementsToAddAfter.Add(varDeclInside);
                    }
                }

                var varDecl = new AstVarDecl(udecl.Type, udecl.Name, initializer as AstExpression, udecl.Documentation, new Location(udecl.Beginning, end));
                varDecl.Attributes.AddRange(attrs);
                varDecl.SpecialKeys.AddRange(udecl.SpecialKeys);
                varDecl.IsImported = inInfo.ExternalMetadata;

                OnExit();
                return varDecl;
            }
            // assing to var
            else if (CheckTokens(inInfo, TokenType.Equal, TokenType.AddEq, TokenType.SubEq, TokenType.MulEq, 
                TokenType.DivEq, TokenType.ModEq, TokenType.HatEq, TokenType.CoalesceEq) && udecl.Name == null)
            {
                var currT = NextToken(inInfo);
                var x = currT.Type;
                string op = null;
                switch (x)
                {
                    case TokenType.AddEq: op = "+"; break;
                    case TokenType.SubEq: op = "-"; break;
                    case TokenType.MulEq: op = "*"; break;
                    case TokenType.DivEq: op = "/"; break;
                    case TokenType.ModEq: op = "%"; break;
                    case TokenType.HatEq: op = "^"; break;
                    case TokenType.CoalesceEq: op = "??"; break;
                }
                SkipNewlines(inInfo);

                var saved = inInfo.AllowMultiplyExpression;
                inInfo.AllowMultiplyExpression = true;
                var val = ParseExpression(inInfo, ref outInfo);
                inInfo.AllowMultiplyExpression = saved;

                if (val is not AstExpression valExpr)
                {
                    ReportMessage(val.Location, [], ErrorCode.Get(CTEN.RightSideVarDeclNotExpr));
                    OnExit();
                    return udecl;
                }

                if (udecl.Type is AstNestedExpr id && currT.Type != TokenType.Equal)
                {
                    // expand ops like 'a += b' into 'a = a + b'
                    AstExpression binOpExpr = new AstBinaryExpr(op, id.GetDeepCopy() as AstNestedExpr, valExpr, new Location(id.Location.Beginning, val.Location.Ending));
                    if (op == "??")
                    {
                        /// WARN!!!: the same as in <see cref="ParseNullCoalescingExpression"/>
                        // creating null comparison
                        var nulll = new AstNullExpr(null, id);
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
            else if (CheckToken(inInfo, TokenType.Semicolon) && udecl.Name != null)
            {
                // do not get the next token
                var varDecl = new AstVarDecl(udecl.Type, udecl.Name, null, udecl.Documentation, new Location(udecl.Beginning, end));
                varDecl.Attributes.AddRange(attrs);
                varDecl.SpecialKeys.AddRange(udecl.SpecialKeys);
                varDecl.IsImported = inInfo.ExternalMetadata;
                varDecl.IsEvent = udecl.IsEvent;

                OnExit();
                return varDecl;
            }
            // func declaration 
            else if (CheckToken(inInfo, TokenType.OpenParen))
            {
                bool isVoidType = udecl.Name == null || (udecl.Type is AstNestedExpr nst2 && nst2.RightPart is AstIdExpr idE && idE.Name == "void");

                var func = ParseFuncDeclaration(inInfo, ref outInfo, null, null, isVoidType);
                func.Attributes.AddRange(attrs);
                func.SpecialKeys.AddRange(udecl.SpecialKeys);
                if (udecl.Name == null)
                {
                    var fncName = (udecl.Type as AstNestedExpr).UnrollToRightPart<AstIdExpr>();

                    // it is ctor/dtor
                    func.Name = fncName.GetCopy();
                    func.Returns = new AstNestedExpr(new AstIdExpr("void"), null);
                    // it is a stor/dtor/ctor
                    // set in MainPart
                    func.ClassFunctionType = Enums.ClassFunctionType.Special;
                }
                else
                {
                    // it is normal func
                    func.Name = udecl.Name;
                    func.Returns = udecl.Type;
                }
                OnExit();
                return func;
            }
            // properties 
            else if (CheckToken(inInfo, TokenType.OpenBrace) || CheckToken(inInfo, TokenType.Arrow))
            {
                var prop = PreparePropertyDecl(udecl, udecl.Documentation, inInfo, ref outInfo);
                prop.Attributes.AddRange(attrs);
                // special keys are added inside PreparePropertyDecl
                OnExit();
                return prop;
            }
            // indexer?
            else if (CheckToken(inInfo, TokenType.OpenBracket) && udecl.Name.Name == "this")
            {
                Consume(inInfo, TokenType.OpenBracket, ErrMsg("symbol '['", "at beginning of indexer param declaration"));
                var par = ParseParameter(inInfo, ref outInfo, false); // no default value for indexer
                var a = PeekToken(inInfo);
                Consume(inInfo, TokenType.CloseBracket, ErrMsg("symbol ']'", "at ending of indexer param declaration"));

                SkipNewlines(inInfo);
                udecl.Name = udecl.Name.GetCopy("indexer__");
                var prop = PreparePropertyDecl(udecl, "", inInfo, ref outInfo) as AstPropertyDecl;
                var indexer = new AstIndexerDecl(prop);
                indexer.IndexerParameter = par;

                return indexer;
            }
            // operator overloads
            else if (CheckToken(inInfo, TokenType.KwOperator))
            {
                // possible operator override
                var result = ParseOperatorOverride(inInfo, ref outInfo, udecl);
                if (result != null)
                {
                    result.Attributes.AddRange(attrs);
                    OnExit();
                    return result;
                }
            }
            else if (udecl.Name == null)
            {
                // just return it further
                return udecl.Type;
            }

            ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.PureUnexpectedToken)); // better error message?
            OnExit();
            return udecl;

            void OnExit()
            {
                inInfo.CurrentUdecl = savedUdecl;
            }
        }
    }
}
