using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstStatement ParseArrayExpr(AstExpression type, TokenLocation beg)
		{
			// by default it is null because the size could not be defined
			// when values are presented
			List<AstExpression> sizeExprs = new List<AstExpression>();

			while (CheckToken(TokenType.OpenBracket) || CheckToken(TokenType.ArrayDef))
			{
				// if there is a size expr 
				if (CheckToken(TokenType.OpenBracket))
				{
					Consume(TokenType.OpenBracket, ErrMsg("[", "at the beggining of array expr"));
					if (!CheckToken(TokenType.CloseBracket))
					{
						var arraySize = ParseExpression(false, false);
						if (arraySize is not AstExpression expr)
						{
							// error here. it has to be an expr
							ReportError(arraySize.Location, $"Expression expected to be as an array size");
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
			}

			// defined only size
			if (CheckToken(TokenType.Semicolon))
			{
				if (sizeExprs.Any(x => x == null))
				{
					// error here. because size was not defined and elements are also were not
					ReportError(type.Location, $"Array creation requires its size or elements to be specified");
				}
				return new AstArrayCreateExpr(type, sizeExprs, new List<AstExpression>(), new Location(beg, CurrentToken.Location.Ending));
			}
			else if (CheckToken(TokenType.OpenBrace))
			{
				var elements = ParseArrayElementsExpression();

				// TODO: pring warning here if sizeExpr is null and elements.Count == 0, that empty array will be created

				// count parsed elements and set the size if the sizeExpr was null
				// sizeExpr ??= new AstNumberExpr(NumberData.FromInt(elements.Count));
				// TODO: rewrite the shite to check elements in nested arrays (because they could be only specified in ndim arrays)
				// some elements in sizeExprs could be null so the elements in this situation have to be specified
				// mb do it in PostPrepareInference?

				return new AstArrayCreateExpr(type, sizeExprs, elements, new Location(beg, CurrentToken.Location.Ending));
			}

			// error here like unexpected token
			ReportError(PeekToken().Location, $"Unexpected token after array creation expression");
			return ParseEmptyExpression();
		}

		private List<AstExpression> ParseArrayElementsExpression()
		{
			var token = NextToken();
			var values = new List<AstExpression>();

			while (true)
			{
				SkipNewlines();
				var next = PeekToken();

				if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
					break;

				var expr = ParseExpression(false);
				if (expr is not AstExpression exprexpr)
				{
					// error here. it has to be
					ReportError(expr.Location, $"Array element expected to be an expression");
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
					ReportError(next.Location, "Unexpected token in array elements expression");
					NextToken();
				}
			}

			Consume(TokenType.CloseBrace, ErrMsg("}", "at end of array expression"));
			return values;
		}
	}
}
