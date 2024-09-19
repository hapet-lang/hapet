using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstParamDecl ParseParameter(bool allowCommaForTuple, bool allowDefaultValue = true)
		{
			AstIdExpr pname = null;
			AstStatement ptype = null;
			AstExpression defaultValue = null;

			TokenLocation beg = null, end = null;

			var e = ParseExpression(allowCommaForTuple);
			beg = e.Beginning;
			SkipNewlines();

			if (e is UnknownDecl udecl)
			{
				pname = udecl.Name as AstIdExpr;
				ptype = udecl.Type;
			}
			else
			{
				// if next token is ident then e is the type of the parameter
				if (CheckToken(TokenType.Identifier))
				{
					SkipNewlines();

					var probName = ParseExpression(allowCommaForTuple);
					if (probName is not AstIdExpr)
					{
						ReportError(probName.Location, $"Parameter name has to be an identifier");
					}
					pname = probName as AstIdExpr;
					ptype = e;
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
					var probDefVal = ParseExpression(allowCommaForTuple);
					if (probDefVal is not AstExpression)
					{
						ReportError(probDefVal.Location, $"Parameter default value has to be an expression");
					}
					defaultValue = probDefVal as AstExpression;
					end = defaultValue.Ending;
				}
			}

			// TODO: doc string???
			return new AstParamDecl(ptype as AstExpression, pname, defaultValue, "", new Location(beg, end));
		}

		private List<AstParamDecl> ParseParameterList(TokenType open, TokenType close, out TokenLocation beg, out TokenLocation end, bool allowDefaultValue = true)
		{
			var parameters = new List<AstParamDecl>();

			beg = Consume(open, ErrMsg("(/[", "at beginning of parameter list")).Location;
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
					ReportError(next.Location, $"Expected ',' or ')/]', got '{next}'");
				}
			}

			end = Consume(close, ErrMsg(")/]", "at end of parameter list")).Location;

			return parameters;
		}

		private AstStatement ParseTupleExpression(bool allowFunctionDeclaration, bool allowCommaForTuple)
		{
			var list = ParseParameterList(TokenType.OpenParen, TokenType.CloseParen, out var beg, out var end, allowDefaultValue: true);

			SkipNewlines();

			// function expression
			// hash identifier for directives
			if (allowFunctionDeclaration && CheckTokens(TokenType.OpenBrace, TokenType.Semicolon))
			{
				return ParseFuncDeclaration(list, new Location(beg, end));
			}

			if (CheckToken(TokenType.Arrow))
			{
				// if only one id is given for a parameter, then this should be used as name, not type
				foreach (var p in list)
				{
					if (p.Name == null && p.Type != null)
					{
						p.Name = p.Type as AstIdExpr;
						if (p.Name == null)
							ReportError(p.Type.Location, $"Lambda argument name must be an identifier");
						p.Type = null;
					}
				}
				return ParseLambdaDeclaration(list, beg, allowCommaForTuple);
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
					var expr = list[0].Type;
					expr.Location = new Location(beg, end);
					return expr;
				}
			}

			return new AstTupleExpr(list, new Location(beg, end));
		}
	}
}
