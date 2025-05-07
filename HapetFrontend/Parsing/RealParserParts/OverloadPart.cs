using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using Newtonsoft.Json.Linq;
using System;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private static readonly TokenType[] AllowedUnOps = { TokenType.Plus, TokenType.Minus, TokenType.Bang, TokenType.Tilda, TokenType.PlusPlus, TokenType.MinusMinus };
        private static readonly TokenType[] AllowedBinOps = { TokenType.Plus, TokenType.Minus, TokenType.ForwardSlash, TokenType.Asterisk, TokenType.Percent, 
            TokenType.Ampersand, TokenType.VerticalSlash, TokenType.Hat, TokenType.GreaterGreater, TokenType.LessLess, TokenType.DoubleEqual, TokenType.NotEqual, 
            TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual, TokenType.LogicalAnd, TokenType.LogicalOr };

        internal AstOverloadDecl ParseOperatorOverride(AstUnknownDecl udecl)
        {
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            OverloadType overloadType = OverloadType.UnaryOperator;
            string op = null;

            List<AstParamDecl> paramDecls = null;
            AstBlockExpr body = null;
            TokenLocation possibleEndLocation = null;

            // kostyl to always get type
            AstExpression returns = udecl.Name ?? udecl.Type;

            // cast override
            if ((CheckToken(TokenType.KwImplicit) || CheckToken(TokenType.KwExplicit)))
            {
                // just check
                var imex = NextToken();
                if (imex.Type == TokenType.KwImplicit)
                    overloadType = OverloadType.ImplicitCast;
                else
                    overloadType = OverloadType.ExplicitCast;

                // always 'cast' name to be able to store the operator in Scope 
                op = "cast";

                Consume(TokenType.KwOperator, ErrMsg("'operator'", "after implicit/explicit cast overloading"));

                // getting cast result type
                var saved1 = inInfo.AllowMultiplyExpression;
                inInfo.AllowMultiplyExpression = false;
                returns = ParseExpression(inInfo, ref outInfo) as AstExpression;
                inInfo.AllowMultiplyExpression = saved1;

                var func = ParseFuncDeclaration(null, null, inInfo, ref outInfo);
                paramDecls = func.Parameters;
                body = func.Body;
                possibleEndLocation = func.Location.Ending;

                if (paramDecls == null)
                    ReportMessage(returns, [], ErrorCode.Get(CTEN.ParamsAfterOverloadOperator));
                else if (paramDecls.Count > 1)
                    ReportMessage(returns, [], ErrorCode.Get(CTEN.TooManyParamsAfterOvOp));
            }
            // this is an operator override
            else if (CheckToken(TokenType.KwOperator))
            {
                // skip 'operator' word
                NextToken();

                var opToken = NextToken();
                switch (opToken.Type)
                {
                    case TokenType.Plus: op = "+"; break;
                    case TokenType.Minus: op = "-"; break;
                    case TokenType.Asterisk: op = "*"; break;
                    case TokenType.ForwardSlash: op = "/"; break;
                    case TokenType.Percent: op = "%"; break;

                    case TokenType.Bang: op = "!"; break;
                    case TokenType.LogicalAnd: op = "&&"; break;
                    case TokenType.LogicalOr: op = "||"; break;

                    case TokenType.DoubleEqual: op = "=="; break;
                    case TokenType.NotEqual: op = "!="; break;
                    case TokenType.Less: op = "<"; break;
                    case TokenType.LessEqual: op = "<="; break;
                    case TokenType.Greater: op = ">"; break;
                    case TokenType.GreaterEqual: op = ">="; break;

                    case TokenType.Tilda: op = "~"; break;
                    case TokenType.Ampersand: op = "&"; break;
                    case TokenType.VerticalSlash: op = "|"; break;
                    case TokenType.Hat: op = "^"; break;
                    case TokenType.GreaterGreater: op = ">>"; break;
                    case TokenType.LessLess: op = "<<"; break;

                    case TokenType.PlusPlus: op = "++"; break;
                    case TokenType.MinusMinus: op = "--"; break;
                }

                // cringe handle >>
                // https://github.com/dotnet/roslyn/blob/62646c22f6bd7b213e7e15dbc0dfadfe47a1e30f/src/Compilers/CSharp/Portable/Parser/Lexer.cs#L4118-L4122
                // https://github.com/dotnet/roslyn/blob/62646c22f6bd7b213e7e15dbc0dfadfe47a1e30f/src/Compilers/CSharp/Portable/Parser/LanguageParser.cs#L11067-L11073
                if (op == ">" && PeekToken().Type == TokenType.Greater)
                {
                    NextToken();
                    op = ">>";
                }

                var func = ParseFuncDeclaration(null, null, inInfo, ref outInfo);
                paramDecls = func.Parameters;
                body = func.Body;
                possibleEndLocation = func.Location.Ending;

                if (paramDecls == null)
                    ReportMessage(opToken.Location, [], ErrorCode.Get(CTEN.ParamsAfterOverloadOperator));
                else if (paramDecls.Count == 1)
                    overloadType = OverloadType.UnaryOperator;
                else if (paramDecls.Count == 2)
                    overloadType = OverloadType.BinaryOperator;
                else
                    ReportMessage(opToken.Location, [], ErrorCode.Get(CTEN.TooManyParamsAfterOvOp));

                if (overloadType == OverloadType.UnaryOperator && !AllowedUnOps.Contains(opToken.Type))
                    ReportMessage(opToken.Location, [], ErrorCode.Get(CTEN.UnexpectedUnOpToOverload));
                else if (overloadType == OverloadType.BinaryOperator && !AllowedBinOps.Contains(opToken.Type))
                    ReportMessage(opToken.Location, [], ErrorCode.Get(CTEN.UnexpectedBinOpToOverload));
            }
            else
            {
                // non of conditions are met
                return null;
            }

            string name = AstOverloadDecl.GenerateName(overloadType, op, returns as AstNestedExpr);
            var endLocation = body == null ? possibleEndLocation : body.Ending;
            var overload = new AstOverloadDecl(paramDecls, returns, body, new AstIdExpr(name), "", new Location(udecl.Beginning, endLocation));

            // set up shite
            overload.OverloadType = overloadType;
            overload.Operator = op;

            overload.SpecialKeys.AddRange(udecl.SpecialKeys);
            overload.Attributes.AddRange(udecl.Attributes);

            return overload;
        }
    }
}
