using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Types;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstStatement ParseArrayExpr(ParserInInfo inInfo, ref ParserOutInfo outInfo, AstExpression type, TokenLocation beg)
        {
            // by default it is null because the size could not be defined
            // when values are presented
            List<AstExpression> sizeExprs = new List<AstExpression>();

            TokenLocation sizesBeg = PeekToken().Location;
            TokenLocation sizesEnd = PeekToken().Location;

            while (CheckToken(TokenType.OpenBracket) || CheckToken(TokenType.ArrayDef))
            {
                // if there is a size expr 
                if (CheckToken(TokenType.OpenBracket))
                {
                    Consume(TokenType.OpenBracket, ErrMsg("[", "at the beggining of array expr"));
                    if (!CheckToken(TokenType.CloseBracket))
                    {
                        var arraySize = ParseExpression(inInfo, ref outInfo);
                        if (arraySize is not AstExpression expr)
                        {
                            // error here. it has to be an expr
                            ReportMessage(arraySize.Location, [], ErrorCode.Get(CTEN.ArraySizeNotExpr));
                            return ParseEmptyExpression();
                        }

                        sizeExprs.Add(expr);
                    }
                    Consume(TokenType.CloseBracket, ErrMsg("]", "at the end of array expr"));
                }
                else
                {
                    // if there is no size expr
                    Consume(TokenType.ArrayDef, ErrMsg("[]", "at the end of array expr"));
                    sizeExprs.Add(null);
                }

                sizesEnd = CurrentToken.Location;
            }

            // check for size exprs
            if (sizeExprs.Count == 0)
            {
                ReportMessage(type.Location, [], ErrorCode.Get(CTEN.ArraySizeNotSpecified));
            }

            SkipNewlines();

            // defined only size
            if (CheckToken(TokenType.Semicolon))
            {
                if (sizeExprs.Any(x => x == null))
                {
                    // error here. because size was not defined and elements are also were not
                    ReportMessage(new Location(type.Location.Beginning, CurrentToken.Location.Ending), [], ErrorCode.Get(CTEN.ArraySizeNotSpecified));
                }
                return new AstArrayCreateExpr(type, sizeExprs, new List<AstExpression>(), new Location(beg, CurrentToken.Location.Ending));
            }
            else if (CheckToken(TokenType.OpenBrace))
            {
                // allow only the last size to be null!!! because in other way it is very hard to prepare
                bool allExceptTheLastAreNotNull = sizeExprs.SkipLast(1).All(x => x != null);
                if (!allExceptTheLastAreNotNull)
                {
                    ReportMessage(new Location(sizesBeg, sizesEnd), [], ErrorCode.Get(CTEN.ArrayNonLastNotSpecified));
                }

                var elements = ParseArrayElementsExpression(inInfo, ref outInfo);

                // print warning here if sizeExpr is null and elements.Count == 0, that empty array will be created
                if (sizeExprs.Last() == null && elements.Count == 0)
                    ReportMessage(new Location(beg, CurrentToken.Location.Ending), [], ErrorCode.Get(CTWN.ArrayEmptyCreation), Entities.ReportType.Warning);

                // count parsed elements and set the size if the sizeExpr was null
                if (sizeExprs.Last() == null)
                    sizeExprs[sizeExprs.Count - 1] = new AstNumberExpr(NumberData.FromInt(elements.Count));

                return new AstArrayCreateExpr(type, sizeExprs, elements, new Location(beg, CurrentToken.Location.Ending));
            }

            // error here like unexpected token
            ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.ArrayUnexpectedToken));
            return ParseEmptyExpression();
        }

        private List<AstExpression> ParseArrayElementsExpression(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var token = NextToken();
            var values = new List<AstExpression>();

            while (true)
            {
                SkipNewlines();
                var next = PeekToken();

                if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
                    break;

                var expr = ParseExpression(inInfo, ref outInfo);
                if (expr is not AstExpression exprexpr)
                {
                    // error here. it has to be
                    ReportMessage(expr.Location, [], ErrorCode.Get(CTEN.ArrayElementNotExpr));
                    break;
                }
                values.Add(exprexpr);

                next = PeekToken();

                if (next.Type == TokenType.NewLine || next.Type == TokenType.Comma)
                {
                    NextToken();
                }
                else if (next.Type == TokenType.CloseBrace)
                {
                    break;
                }
                else
                {
                    ReportMessage(next.Location, [], ErrorCode.Get(CTEN.ArrayElementsUnexpectedToken));
                    NextToken();
                }
            }

            Consume(TokenType.CloseBrace, ErrMsg("}", "at end of array expression"));
            return values;
        }
    }
}
