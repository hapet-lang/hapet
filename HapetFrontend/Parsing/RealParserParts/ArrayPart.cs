using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstStatement ParseArrayExpr(AstNestedExpr typeName, TokenLocation beg)
		{
			Consume(TokenType.OpenBracket, ErrMsg("[", "at the beggining of array expr"));

			// by default it is null because the size could not be defined
			// when values are presented
			AstExpression sizeExpr = null;

			if (!CheckToken(TokenType.CloseBracket))
			{
				var arraySize = ParseExpression(false, false);
				if (arraySize is not AstExpression expr)
				{
					// TODO: error here. it has to be an expr
					return ParseEmptyExpression();
				}

				sizeExpr = expr;
			}

			Consume(TokenType.CloseBracket, ErrMsg("]", "at the end of array expr"));

			// defined only size
			if (CheckToken(TokenType.Semicolon))
			{
				if (sizeExpr == null)
				{
					// TODO: error here. because size was not defined and elements are also were not
				}
				return new AstArrayExpr(typeName, sizeExpr, new List<AstExpression>(), new Location(beg, CurrentToken.Location.Ending));
			}
			else if (CheckToken(TokenType.OpenBrace))
			{
				var elements = ParseArrayElementsExpression();

				// count parsed elements and set the size if the sizeExpr was null
				sizeExpr ??= new AstNumberExpr(NumberData.FromInt(elements.Count));

				return new AstArrayExpr(typeName, sizeExpr, elements, new Location(beg, CurrentToken.Location.Ending));
			}

			// TODO: error here like unexpected token
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
					// TODO: error here. it has to be
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
