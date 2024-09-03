using Frontend.Ast.Expressions;
using Frontend.Ast;
using Frontend.Parsing.Entities;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		private AstExpression ParseTupleExpression(bool allowFunctionExpression, bool allowCommaForTuple)
		{
			var list = ParseParameterList(TokenType.OpenParen, TokenType.CloseParen, out var beg, out var end, allowDefaultValue: true);
			SkipNewlines();
			// function expression
			// hash identifier for directives
			if (allowFunctionExpression && CheckTokens(TokenType.OpenBrace, TokenType.SharpIdentifier, TokenType.Semicolon))
			{
				return ParseFuncExpr(list, new Location(beg, end));
			}

			if (CheckToken(TokenType.Arrow))
			{
				// if only one id is given for a parameter, then this should be used as name, not type
				foreach (var p in list)
				{
					if (p.Name == null && p.TypeExpr != null)
					{
						p.Name = p.TypeExpr as AstIdExpr;
						if (p.Name == null)
							ReportError(p.TypeExpr.Location, $"Lambda argument name must be an identifier");
						p.TypeExpr = null;
					}
				}
				return ParseLambdaExpr(list, beg, allowCommaForTuple);
			}

			bool isType = false;
			foreach (var v in list)
			{
				if (v.Name != null)
					isType = true;
			}

			if (!isType)
			{
				if (list.Count == 1)
				{
					var expr = list[0].TypeExpr;
					expr.Location = new Location(beg, end);
					return expr;
				}
			}

			return new AstTupleExpr(list, new Location(beg, end));
		}

		private List<AstParameter> ParseParameterList(TokenType open, TokenType close, out TokenLocation beg, out TokenLocation end, bool allowDefaultValue = true)
		{
			var parameters = new List<AstParameter>();

			beg = Consume(open, ErrMsg("(", "at beginning of parameter list")).Location;
			SkipNewlines();

			while (true)
			{
				var next = PeekToken();
				if (next.Type == close || next.Type == TokenType.EOF)
					break;

				var a = ParseParameter(false, allowDefaultValue);
				parameters.Add(a);

				SkipNewlines();
				next = PeekToken();
				if (next.Type == TokenType.Comma)
				{
					NextToken();
					SkipNewlines();
				}
				else if (next.Type == close)
					break;
				else
				{
					NextToken();
					SkipNewlines();
					ReportError(next.Location, $"Expected ',' or ')', got '{next}'");
				}
			}

			end = Consume(close, ErrMsg(")", "at end of parameter list")).Location;

			return parameters;
		}

		private AstParameter ParseParameter(bool allowCommaForTuple, bool allowDefaultValue = true)
		{
			AstIdExpr pname = null;
			AstExpression ptype = null;
			AstExpression defaultValue = null;

			TokenLocation beg = null, end = null;

			var e = ParseExpression(allowCommaForTuple);
			beg = e.Beginning;
			SkipNewlines();

			if (e is AstTypeWithNameExpr typeWithName && typeWithName.TypeAndValue.name is AstIdExpr nm)
			{
				ptype = typeWithName.TypeAndValue.tp;
				pname = nm;
			}
			else
			{
				// if next token is ident then e is the Type of the parameter
				if (CheckToken(TokenType.Identifier))
				{
					ptype = e;

					SkipNewlines();

					pname = ParseIdentifierExpr();
				}
				else
				{
					ptype = e;
				}
			}

			end = ptype.Ending;

			if (allowDefaultValue)
			{
				// optional default value
				SkipNewlines();
				if (CheckToken(TokenType.Equal))
				{
					NextToken();
					SkipNewlines();
					defaultValue = ParseExpression(allowCommaForTuple);
					end = defaultValue.Ending;
				}
			}

			return new AstParameter(pname, ptype, defaultValue, new Location(beg, end));
		}
	}
}
