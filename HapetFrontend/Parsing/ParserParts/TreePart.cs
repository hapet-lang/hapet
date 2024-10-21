using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		public AstStatement ParseExpression(bool allowCommaForTuple, bool allowFunctionDeclaration = false, ErrorMessageResolver errorMessage = null)
		{
			errorMessage = errorMessage ?? (t => $"Unexpected token '{t}' in expression");

			var expr = ParseIsExpression(false, allowFunctionDeclaration, errorMessage);

			return expr;
		}

		private AstStatement ParseIsExpression(bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver errorMessage)
		{
			var lhs = ParseAsExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
			AstStatement rhs = null;

			while (CheckToken(TokenType.KwIs))
			{
				var _is = NextToken();
				SkipNewlines();
				rhs = ParseAsExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
				lhs = new AstBinaryExpr("is", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
			return lhs;
		}

		private AstStatement ParseAsExpression(bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver errorMessage)
		{
			var lhs = ParseInExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
			AstStatement rhs = null;

			while (CheckToken(TokenType.KwAs))
			{
				var _as = NextToken();
				SkipNewlines();
				rhs = ParseInExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
				// lhs = new AstBinaryExpr("as", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
				// TODO: do i really have to use Cast?
				lhs = new AstCastExpr(rhs, lhs, new Location(lhs.Beginning, rhs.Ending));
			}
			return lhs;
		}

		private AstStatement ParseInExpression(bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver errorMessage)
		{
			var lhs = ParseOrExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
			AstStatement rhs = null;

			while (CheckToken(TokenType.KwIn))
			{
				var _in = NextToken();
				SkipNewlines();
				rhs = ParseOrExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
				lhs = new AstBinaryExpr("in", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
			return lhs;
		}

		[DebuggerStepThrough]
		private AstStatement ParseOrExpression(bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseAndExpression, allowCommaForTuple, allowFunctionDeclaration, e,
				(TokenType.LogicalOr, "||"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseAndExpression(bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseComparisonExpression, allowCommaForTuple, allowFunctionDeclaration, e,
				(TokenType.LogicalAnd, "&&"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseComparisonExpression(bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseAddSubExpression, allowCommaForTuple, allowFunctionDeclaration, e,
				(TokenType.Less, "<"),
				(TokenType.LessEqual, "<="),
				(TokenType.Greater, ">"),
				(TokenType.GreaterEqual, ">="),
				(TokenType.DoubleEqual, "=="),
				(TokenType.NotEqual, "!="));
		}

		[DebuggerStepThrough]
		private AstStatement ParseAddSubExpression(bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseMulDivExpression, allowCommaForTuple, allowFunctionDeclaration, e,
				(TokenType.Plus, "+"),
				(TokenType.Minus, "-"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseMulDivExpression(bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseUnaryExpression, allowCommaForTuple, allowFunctionDeclaration, e,
				(TokenType.Asterisk, "*"),
				(TokenType.ForwardSlash, "/"),
				(TokenType.Percent, "%"));
		}

		[Obsolete("Use ParseMulDivExpression")]
		[DebuggerStepThrough]
		private AstStatement ParseBinaryExpression(bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseUnaryExpression, allowCommaForTuple, allowFunctionDeclaration, e,
				(TokenType.Asterisk, "*"),
				(TokenType.ForwardSlash, "/"),
				(TokenType.Percent, "%"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseBinaryLeftAssociativeExpression(ExpressionParser sub, bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver errorMessage, params (TokenType, string)[] types)
		{
			return ParseLeftAssociativeExpression(sub, allowCommaForTuple, allowFunctionDeclaration, errorMessage, type =>
			{
				foreach (var (t, o) in types)
				{
					if (t == type)
						return o;
				}
				return null;
			});
		}

		private AstStatement ParseLeftAssociativeExpression(
			ExpressionParser sub,
			bool allowCommaForTuple,
			bool allowFunctionDeclaration,
			ErrorMessageResolver errorMessage,
			Func<TokenType, string> tokenMapping)
		{
			var lhs = sub(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
			AstStatement rhs = null;

			while (true)
			{
				var next = PeekToken();

				var op = tokenMapping(next.Type);
				if (op == null)
				{
					return lhs;
				}

				NextToken();
				SkipNewlines();
				rhs = sub(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
				lhs = new AstBinaryExpr(op, lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
		}

		private AstStatement ParseUnaryExpression(bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver errorMessage = null)
		{
			var next = PeekToken();
			if (next.Type == TokenType.Ampersand)
			{
				NextToken();
				SkipNewlines();

				var sub = ParseUnaryExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
				if (sub is not AstExpression expr)
				{
					ReportError(sub.Location, $"Expression expected after '&'");
					return sub;
				}
				return new AstAddressOfExpr(expr, new Location(next.Location, sub.Ending));
			}
			else if (next.Type == TokenType.Asterisk)
			{
				NextToken();
				SkipNewlines();
				var sub = ParseUnaryExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
				if (sub is not AstExpression expr)
				{
					ReportError(sub.Location, $"Expression expected after '*'");
					return sub;
				}
				return new AstPointerExpr(expr, true, new Location(next.Location, sub.Ending));
			}
			else if (next.Type == TokenType.Minus || next.Type == TokenType.Plus)
			{
				NextToken();
				SkipNewlines();
				var sub = ParseUnaryExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
				string op = "";
				switch (next.Type)
				{
					case TokenType.Plus: op = "+"; break;
					case TokenType.Minus: op = "-"; break;
				}
				return new AstUnaryExpr(op, sub, new Location(next.Location, sub.Ending));
			}
			else if (next.Type == TokenType.Bang)
			{
				NextToken();
				SkipNewlines();
				var sub = ParseUnaryExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
				return new AstUnaryExpr("!", sub, new Location(next.Location, sub.Ending));
			}

			return ParsePostUnaryExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
		}

		private AstStatement ParsePostUnaryExpression(bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver errorMessage)
		{
			var expr = ParseAtomicExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);

			bool breakLoop = false;
			while (!breakLoop)
			{
				switch (PeekToken().Type)
				{
					case TokenType.OpenParen:
						{
							// if it is func decl, not call
							if (allowFunctionDeclaration)
							{
								breakLoop = true;
								break;
							}

							var args = ParseArgumentList(out var end);
							if (expr is not AstNestedExpr nestExpr)
							{
								ReportError(expr.Location, $"Indentifier expected");
								return expr;
							}
							if (nestExpr.RightPart is not AstIdExpr idExpr)
							{
								ReportError(nestExpr.Location, $"Indentifier expected as the func name");
								return expr;
							}
							expr = new AstCallExpr(nestExpr.LeftPart, idExpr.GetCopy(), args, new Location(expr.Beginning, end));

							// TODO: check for dots after this!!! there could be a.asd().asd().ddd().d.lll()
						}
						break;

					case TokenType.OpenBracket:
						{
							NextToken();
							SkipNewlines();

							var args = new List<AstStatement>();
							while (true)
							{
								var next = PeekToken();
								if (next.Type == TokenType.CloseBracket || next.Type == TokenType.EOF)
									break;
								args.Add(ParseExpression(false));
								SkipNewlines();

								next = PeekToken();
								if (next.Type == TokenType.Comma)
								{
									NextToken();
									SkipNewlines();
								}
								else if (next.Type == TokenType.CloseBracket)
									break;
								else
								{
									NextToken();
									ReportError(next.Location, $"Failed to parse operator [], expected ',' or ']'");
									//RecoverExpression();
								}
							}
							var end = Consume(TokenType.CloseBracket, ErrMsg("]", "at end of [] operator")).Location;
							if (args.Count == 0)
							{
								ReportError(end, "At least one argument required");
								args.Add(ParseEmptyExpression());
							}
							else if (args.Count > 1)
							{
								// TODO: mb allow them multiple args in []?
								ReportError(end, "Too many arguments passed");
							}

							if (expr is not AstNestedExpr nestExpr)
							{
								ReportError(expr.Location, $"Indentifier expected before an array access");
								return expr;
							}

							if (args.First() is not AstExpression firstExpr)
							{
								ReportError(args.First().Location, $"Expression expected as index of element in [...]");
								return expr;
							}
							var arrAcc = new AstArrayAccessExpr(nestExpr, firstExpr, new Location(expr.Beginning, end));
							expr = new AstNestedExpr(arrAcc, null, new Location(expr.Beginning, end));
						}
						break;

					default:
						return expr;
				}
			}
			return expr;
		}

		private AstStatement ParseAtomicExpression(bool allowCommaForTuple, bool allowFunctionDeclaration, ErrorMessageResolver errorMessage)
		{
			var token = PeekToken();
			switch (token.Type)
			{
				case TokenType.KwBreak:
				case TokenType.KwContinue:
					NextToken();
					return new AstBreakContStmt(token.Type == TokenType.KwBreak, new Location(token.Location));

				case TokenType.KwDefault:
					NextToken();
					return new AstDefaultExpr(new Location(token.Location));

				case TokenType.KwNull:
					NextToken();
					return new AstNullExpr(new Location(token.Location));

				// TODO: what should i do here
				//case TokenType.OpenBracket:
				//	return ParseArrayOrSliceExpression();

				case TokenType.KwNew:
					{
						return ParseNewExpression();
					}

				case TokenType.Identifier:
					{
						var id = ParseIdentifierExpression();

						if (CheckToken(TokenType.ArrayDef))
						{
							if (id.RightPart is not AstIdExpr idExpr)
							{
								ReportError(id.Location, $"Indentifier expected as the array type");
								return id;
							}
							// probably array def (i hope so)
							idExpr.Name += "[]";
							idExpr.Location.Ending.End += 2;
							NextToken();
						}

						// if it is a pointer type
						while (CheckToken(TokenType.Asterisk))
						{
							NextToken();
							var ptrExpr = new AstPointerExpr(id.RightPart, false, new Location(id.RightPart.Beginning, CurrentToken.Location.Ending));
							id.RightPart = ptrExpr;
						}

						if (CheckToken(TokenType.Identifier))
						{
							var name = ParseIdentifierExpression(allowDots: false);
							if (name.RightPart is not AstIdExpr idExpr)
							{
								ReportError(id.Location, $"Identifier expected as a name of declaration");
								return id;
							}
							return new UnknownDecl(id, idExpr, new Location(token.Location));
						}

						return id;
					}

				case TokenType.Tilda:
					{
						NextToken();
						if (!CheckToken(TokenType.Identifier))
						{
							ReportError(PeekToken().Location, $"Identifier expected after '~'");
							return ParseEmptyExpression();
						}

						var expr = ParseExpression(allowCommaForTuple, allowFunctionDeclaration, errorMessage);
						if (expr is AstIdExpr idExpr)
						{
							idExpr.Suffix = "~";
						}
						else
						{
							ReportError(PeekToken().Location, $"This type of expr was not expected after '~'");
						}
						return expr;
					}

				case TokenType.StringLiteral:
					NextToken();
					return new AstStringExpr((string)token.Data, token.Suffix, new Location(token.Location));

				case TokenType.CharLiteral:
					NextToken();
					return new AstCharExpr((string)token.Data, new Location(token.Location));

				case TokenType.NumberLiteral:
					NextToken();
					return new AstNumberExpr((NumberData)token.Data, token.Suffix, new Location(token.Location));

				case TokenType.KwTrue:
					NextToken();
					return new AstBoolExpr(true, new Location(token.Location));

				case TokenType.KwFalse:
					NextToken();
					return new AstBoolExpr(false, new Location(token.Location));

				case TokenType.OpenBrace:
					return ParseBlockExpression();
				
				case TokenType.KwIf:
					return ParseIfStatement();

				// TODO: ...
				//case TokenType.KwSwitch:
				//	return ParseSwitchExpression();

				case TokenType.OpenParen:
					return ParseTupleExpression(allowFunctionDeclaration, allowCommaForTuple);

				// TODO: ...
				//case TokenType.KwStruct:
				//	return ParseStructDeclaration();

				//case TokenType.KwEnum:
				//	return ParseEnumDeclaration();

				case TokenType.KwClass:
					return ParseClassDeclaration();

				// custom shite
				case TokenType.KwPublic:
				case TokenType.KwProtected:
				case TokenType.KwPrivate:
					return ParseAccessKeys(token.Type);

				case TokenType.KwAsync:
					return ParseSyncKeys(token.Type);

				case TokenType.KwStatic:
					return ParseInstancingKeys(token.Type);

				case TokenType.KwAbstract:
				case TokenType.KwVirtual:
				case TokenType.KwOverride:
				case TokenType.KwPartial:
				case TokenType.KwExtern:
					return ParseImplementationKeys(token.Type);

				default:
					//NextToken();
					ReportError(token.Location, errorMessage?.Invoke(token) ?? $"Failed to parse expression, unpexpected token ({token.Type}) {token.Data}");
					return ParseEmptyExpression();
			}
		}
	}
}
