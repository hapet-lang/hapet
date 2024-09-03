using Frontend.Ast;
using Frontend.Ast.Declarations;
using Frontend.Ast.Expressions;
using Frontend.Parsing.Entities;
using System.Diagnostics;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		private delegate AstExpression ExpressionParser(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e);

		public AstExpression ParseExpression(bool allowCommaForTuple, bool allowFunctionExpression = false, ErrorMessageResolver errorMessage = null)
		{
			errorMessage = errorMessage ?? (t => $"Unexpected token '{t}' in expression");

			var expr = ParseIsExpression(false, allowFunctionExpression, errorMessage);

			if (allowCommaForTuple)
			{
				List<AstParameter> list = null;
				while (CheckToken(TokenType.Comma))
				{
					if (list == null)
					{
						list = new List<AstParameter>();
						list.Add(new AstParameter(null, expr, null, expr));
					}

					NextToken();

					expr = ParseIsExpression(false, allowFunctionExpression, errorMessage);
					list.Add(new AstParameter(null, expr, null, expr));
				}

				if (list != null)
					expr = new AstTupleExpr(list, new Location(list.First().Beginning, list.Last().Ending));
			}

			return expr;
		}

		private AstExpression ParseIsExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage)
		{
			var lhs = ParseInExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
			AstExpression rhs = null;

			while (CheckToken(TokenType.KwIs))
			{
				var _is = NextToken();
				SkipNewlines();
				rhs = ParseInExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
				lhs = new AstBinaryExpr("is", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
			return lhs;
		}

		private AstExpression ParseInExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage)
		{
			var lhs = ParseAsExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
			AstExpression rhs = null;

			while (CheckToken(TokenType.KwIn))
			{
				var _is = NextToken();
				SkipNewlines();
				rhs = ParseAsExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
				lhs = new AstBinaryExpr("in", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
			return lhs;
		}

		private AstExpression ParseAsExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage)
		{
			var lhs = ParseOrExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
			AstExpression rhs = null;

			while (CheckToken(TokenType.KwAs))
			{
				var _is = NextToken();
				SkipNewlines();
				rhs = ParseOrExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
				lhs = new AstBinaryExpr("as", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
			return lhs;
		}

		[DebuggerStepThrough]
		private AstExpression ParseOrExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseAndExpression, allowCommaForTuple, allowFunctionExpression, e,
				(TokenType.LogicalOr, "||"));
		}

		[DebuggerStepThrough]
		private AstExpression ParseAndExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseComparisonExpression, allowCommaForTuple, allowFunctionExpression, e,
				(TokenType.LogicalAnd, "&&"));
		}

		[DebuggerStepThrough]
		private AstExpression ParseComparisonExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseRangeExpression, allowCommaForTuple, allowFunctionExpression, e,
				(TokenType.Less, "<"),
				(TokenType.LessEqual, "<="),
				(TokenType.Greater, ">"),
				(TokenType.GreaterEqual, ">="),
				(TokenType.DoubleEqual, "=="),
				(TokenType.NotEqual, "!="));
		}

		//[DebuggerStepThrough]
		private AstExpression ParseRangeExpressionNoStart(AstExpression lhs, bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			if (CheckToken(TokenType.PeriodPeriod))
			{
				var loc = NextToken().Location;
				var leftLoc = lhs?.Beginning ?? loc;
				bool inclusive = false;

				if (CheckToken(TokenType.Equal))
				{
					loc = NextToken().Location;
					inclusive = true;
				}

				if (IsExprToken())
				{
					var rhs = ParseAddSubExpression(allowCommaForTuple, allowFunctionExpression, e);
					return new AstRangeExpr(lhs, rhs, inclusive, new Location(leftLoc, rhs.Ending));
				}
				else
				{
					return new AstRangeExpr(lhs, null, inclusive, new Location(leftLoc, loc));
				}
			}

			return lhs;
		}

		private AstExpression ParseRangeExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			var lhs = ParseAddSubExpression(allowCommaForTuple, allowFunctionExpression, e);
			return ParseRangeExpressionNoStart(lhs, allowCommaForTuple, allowFunctionExpression, e);
		}

		[DebuggerStepThrough]
		private AstExpression ParseAddSubExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseMulDivExpression, allowCommaForTuple, allowFunctionExpression, e,
				(TokenType.Plus, "+"),
				(TokenType.Minus, "-"));
		}

		[DebuggerStepThrough]
		private AstExpression ParseMulDivExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseUnaryExpression, allowCommaForTuple, allowFunctionExpression, e,
				(TokenType.Asterisk, "*"),
				(TokenType.ForwardSlash, "/"),
				(TokenType.Percent, "%"));
		}

		[DebuggerStepThrough]
		private AstExpression ParseBinaryLeftAssociativeExpression(ExpressionParser sub, bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage, params (TokenType, string)[] types)
		{
			return ParseLeftAssociativeExpression(sub, allowCommaForTuple, allowFunctionExpression, errorMessage, type =>
			{
				foreach (var (t, o) in types)
				{
					if (t == type)
						return o;
				}

				return null;
			});
		}

		private AstExpression ParseLeftAssociativeExpression(
			ExpressionParser sub,
			bool allowCommaForTuple,
			bool allowFunctionExpression,
			ErrorMessageResolver errorMessage,
			Func<TokenType, string> tokenMapping)
		{
			var lhs = sub(allowCommaForTuple, allowFunctionExpression, errorMessage);
			AstExpression rhs = null;

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
				rhs = sub(allowCommaForTuple, allowFunctionExpression, errorMessage);
				lhs = new AstBinaryExpr(op, lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
		}

		private AstExpression ParseUnaryExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage = null)
		{
			var next = PeekToken();
			if (next.Type == TokenType.Hat)
			{
				NextToken();
				SkipNewlines();

				//bool mutable = false;
				//if (CheckToken(TokenType.KwMut))
				//{
				//	NextToken();
				//	SkipNewlines();
				//	mutable = true;
				//}

				var sub = ParseUnaryExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
				return new AstAddressOfExpr(sub, false, new Location(next.Location, sub.Ending));
			}
			else if (next.Type == TokenType.Asterisk)
			{
				NextToken();
				SkipNewlines();
				var sub = ParseUnaryExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
				return new AstDereferenceExpr(sub, new Location(next.Location, sub.Ending));
			}
			else if (next.Type == TokenType.Minus || next.Type == TokenType.Plus)
			{
				NextToken();
				SkipNewlines();
				var sub = ParseUnaryExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
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
				var sub = ParseUnaryExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
				return new AstUnaryExpr("!", sub, new Location(next.Location, sub.Ending));
			}

			return ParsePostUnaryExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
		}

		private AstExpression ParsePostUnaryExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage)
		{
			var expr = ParseAtomicExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);

			while (true)
			{
				switch (PeekToken().Type)
				{
					case TokenType.OpenParen:
						{
							NextToken();
							SkipNewlines();
							var args = new List<AstArgument>();
							while (true)
							{
								var next = PeekToken();
								if (next.Type == TokenType.CloseParen || next.Type == TokenType.EOF)
									break;
								args.Add(ParseArgumentExpression());

								next = PeekToken();
								if (next.Type == TokenType.NewLine)
								{
									NextToken();
								}
								else if (next.Type == TokenType.Comma)
								{
									NextToken();
									SkipNewlines();
								}
								else if (next.Type == TokenType.CloseParen)
									break;
								else
								{
									NextToken();
									ReportError(next.Location, $"Failed to parse function call, expected ',' or ')'");
									//RecoverExpression();
								}
							}
							var end = Consume(TokenType.CloseParen, ErrMsg(")", "at end of function call")).Location;
							expr = new AstCallExpr(expr, args, new Location(expr.Beginning, end));
						}
						break;

					case TokenType.OpenBracket:
						{
							NextToken();
							SkipNewlines();

							var args = new List<AstExpression>();
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
								// TODO: it could be an array type
								// ReportError(end, "At least one argument required");
								args.Add(ParseEmptyExpression());
							}
							expr = new AstArrayAccessExpr(expr, args, new Location(expr.Beginning, end));
						}
						break;

					//case TokenType.Period:
					//	{
					//		NextToken();
					//		SkipNewlines();
					//		var right = ParseIdentifierExpr(ErrMsg("identifier", "after ."));

					//		expr = new AstDotExpr(expr, right, new Location(expr.Beginning, right.End));
					//		break;
					//	}

					default:
						return expr;
				}
			}
		}

		private AstExpression ParseAtomicExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage)
		{
			var token = PeekToken();
			switch (token.Type)
			{
				//case TokenType.Period:
				//	{
				//		var beg = NextToken().Location;
				//		var expr = ParseIdentifierExpr();
				//		return new AstDotExpr(null, expr, new Location(beg, expr.End));
				//	}

				case TokenType.PeriodPeriod:
					return ParseRangeExpressionNoStart(null, allowCommaForTuple, allowFunctionExpression, errorMessage);

				//case TokenType.KwGeneric:
				//	return ParseGenericExpression(allowCommaForTuple, allowFunctionExpression);

				case TokenType.KwUsing:
					return ParseUsingExpr();

				//case TokenType.KwBreak:
				//	return ParseBreakExpr();

				//case TokenType.KwContinue:
				//	return ParseContinueExpr();

				//case TokenType.KwDefault:
				//	NextToken();
				//	return new AstDefaultExpr(new Location(token.Location));

				//case TokenType.KwNull:
				//	NextToken();
				//	return new AstNullExpr(new Location(token.Location));

				//case TokenType.AtSignIdentifier:
				//	{
				//		NextToken();
				//		var args = ParseArgumentList(out var end);
				//		var name = new AstIdExpr((string)token.Data, false, new Location(token.Location));
				//		return new AstCompCallExpr(name, args, new Location(token.Location, end));
				//	}

				//case TokenType.OpenBracket:
				//	return ParseArrayOrSliceExpression();

				case TokenType.DollarIdentifier:
					NextToken();
					return new AstIdExpr((string)token.Data, true, new Location(token.Location));

				case TokenType.Identifier:
					{
						//NextToken();
						//var id = new AstIdExpr((string)token.Data, false, new Location(token.Location));
						////if (CheckToken(TokenType.Arrow))
						////	return ParseLambdaExpr(
						////		new List<AstParameter> { new AstParameter(id, null, null, false, id.Location) },
						////		id.Beginning, allowCommaForTuple);
						////else
						//	return id;

						var currId = ParseIdentifierExpr();
						if (CheckToken(TokenType.OpenBracket))
						{
							var arrShite = ParseExpression(allowCommaForTuple);
							if (arrShite is AstArrayAccessExpr acsA && acsA.Arguments.Count == 1 && acsA.Arguments[0] is AstEmptyExpr)
							{
								currId.IsArray = true;
							}
							// TODO: idk what to do here :)))
						}

						if (CheckToken(TokenType.Identifier))
						{
							// currId is a type and new one is a name
							var nameId = ParseIdentifierExpr();
							if (CheckToken(TokenType.OpenParen))
							{
								// if it is method decl
								var func = ParseExpression(true, true);
								if (func is AstFuncExpr realFunc)
								{
									// setting the return type of the func
									realFunc.Name = nameId.Name;
									realFunc.ReturnTypeExpr = new AstParameter(null, currId, null, currId.Location);
								}
								// TODO: lambda won't be parsed here because it has no name
								else if (func is AstLambdaExpr realLambda)
								{
									// setting the return type of the lambda
									realLambda.ReturnTypeExpr = currId;
								}
								else
								{
									ReportError(func.Location, $"Unknown behaviour detected. This should be a func or lambda");
								}
								return func;
							}
							else
							{
								// it is a variable or a field/property and so on
								// SHOULD BE EDITED IN ParseDeclaration
								return new AstTypeWithNameExpr(currId, nameId, nameId.Location);
							}
						}
						return currId; // just identifier
					}

					// TODO: uncomment
				//case TokenType.StringLiteral:
				//	NextToken();
				//	return new AstStringLiteral((string)token.Data, token.Suffix, new Location(token.Location));

				//case TokenType.CharLiteral:
				//	NextToken();
				//	return new AstCharLiteral((string)token.Data, new Location(token.Location));

				//case TokenType.NumberLiteral:
				//	NextToken();
				//	return new AstNumberExpr((NumberData)token.Data, token.Suffix, new Location(token.Location));

				//case TokenType.KwTrue:
				//	NextToken();
				//	return new AstBoolExpr(true, new Location(token.Location));

				//case TokenType.KwFalse:
				//	NextToken();
				//	return new AstBoolExpr(false, new Location(token.Location));

				case TokenType.OpenBrace:
					return ParseBlockExpr();

				//case TokenType.KwIf:
				//	return ParseIfExpr(allowCommaForTuple);

				//case TokenType.KwSwitch:
				//	return ParseMatchExpr();

				case TokenType.OpenParen:
					return ParseTupleExpression(allowFunctionExpression, allowCommaForTuple);
					//{
					//	var start = NextToken().location;
					//	SkipNewlines();
					//	if (CheckToken(TokenType.ClosingParen))
					//	{
					//		var end = NextToken().location;
					//		return new AstTupleExpr(new List<AstParameter>(), new Location(start, end));
					//	}
					//	else
					//	{
					//		var expr = ParseExpression(true);
					//		var end = ConsumeUntil(TokenType.ClosingParen, ErrMsg(")", "at end of tuple")).location;
					//		return expr;
					//	}
					//}

				//case TokenType.Ampersand:
				//	NextToken();
				//	SkipNewlines();
				//	//bool mutable = false;
				//	//if (CheckToken(TokenType.KwMut))
				//	//{
				//	//	NextToken();
				//	//	SkipNewlines();
				//	//	mutable = true;
				//	//}

				//	var target = ParseExpression(allowCommaForTuple);
				//	return new AstReferenceTypeExpr(target, mutable, new Location(token.Location, target.Ending));

				// TODO: probably won't be parsed from here
				//case TokenType.Kwfn:
				//case TokenType.KwFn:
				//	return ParseFunctionTypeExpr(allowCommaForTuple);


				// TODO: do i need it?
				//case TokenType.KwCast:
				//	{
				//		var beg = token.location;
				//		AstExpression type = null;

				//		NextToken();
				//		SkipNewlines();

				//		var next = PeekToken();
				//		if (next.type == TokenType.OpenParen)
				//		{
				//			NextToken();
				//			SkipNewlines();
				//			type = ParseExpression(true);
				//			SkipNewlines();
				//			Consume(TokenType.ClosingParen, ErrMsg("')'", "after type in cast expression"));
				//			SkipNewlines();
				//		}

				//		var sub = ParseExpression(allowCommaForTuple);
				//		return new AstCastExpr(type, sub, new Location(beg, sub.End));
				//	}

				//case TokenType.KwStruct:
				//	return ParseStructTypeExpression();

				//case TokenType.KwEnum:
				//	return ParseEnumTypeExpression();

				case TokenType.KwClass:
					return ParseClassTypeExpression();

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
					return ParseImplementationKeys(token.Type);

				default:
					//NextToken();
					ReportError(token.Location, errorMessage?.Invoke(token) ?? $"Failed to parse expression, unpexpected token ({token.Type}) {token.Data}");
					return ParseEmptyExpression();
			}
		}
	}
}
