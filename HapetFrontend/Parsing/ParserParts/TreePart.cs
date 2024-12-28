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
		public AstStatement ParseExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration = false,
			MessageResolver message = null, 
			bool allowPointerExpressions = false)
		{
			message = message ?? (t => $"Unexpected token '{t}' in expression");

			var expr = ParseOrExpression(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);

			return expr;
		}

		[DebuggerStepThrough]
		private AstStatement ParseOrExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowPointerExpressions = false)
		{
			return ParseBinaryLeftAssociativeExpression(ParseAndExpression, allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions,
				(TokenType.LogicalOr, "||"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseAndExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowPointerExpressions = false)
		{
			return ParseBinaryLeftAssociativeExpression(ParseIsExpression, allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions,
				(TokenType.LogicalAnd, "&&"));
		}

		private AstStatement ParseIsExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowPointerExpressions = false)
		{
			var lhs = ParseAsExpression(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
			AstStatement rhs = null;

			while (CheckToken(TokenType.KwIs))
			{
				var _is = NextToken();
				SkipNewlines();
				rhs = ParseAsExpression(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
				lhs = new AstBinaryExpr("is", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
			return lhs;
		}

		private AstStatement ParseAsExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowPointerExpressions = false)
		{
			var lhs = ParseInExpression(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
			AstStatement rhs = null;

			while (CheckToken(TokenType.KwAs))
			{
				var _as = NextToken();
				SkipNewlines();
				rhs = ParseInExpression(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
				lhs = new AstBinaryExpr("as", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
			return lhs;
		}

		private AstStatement ParseInExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowPointerExpressions = false)
		{
			var lhs = ParseComparisonExpression(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
			AstStatement rhs = null;

			while (CheckToken(TokenType.KwIn))
			{
				var _in = NextToken();
				SkipNewlines();
				rhs = ParseComparisonExpression(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
				lhs = new AstBinaryExpr("in", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
			return lhs;
		}

		[DebuggerStepThrough]
		private AstStatement ParseComparisonExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowPointerExpressions = false)
		{
			return ParseBinaryLeftAssociativeExpression(ParseBitAndOrExpression, allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions,
				(TokenType.Less, "<"),
				(TokenType.LessEqual, "<="),
				(TokenType.Greater, ">"),
				(TokenType.GreaterEqual, ">="),
				(TokenType.DoubleEqual, "=="),
				(TokenType.NotEqual, "!="));
		}

		[DebuggerStepThrough]
		private AstStatement ParseBitAndOrExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowPointerExpressions = false)
		{
			return ParseBinaryLeftAssociativeExpression(ParseBitShiftExpression, allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions,
				(TokenType.Ampersand, "&"),
				(TokenType.VerticalSlash, "|"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseBitShiftExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowPointerExpressions = false)
		{
			return ParseBinaryLeftAssociativeExpression(ParseAddSubExpression, allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions,
				(TokenType.LessLess, "<<"),
				(TokenType.GreaterGreater, ">>"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseAddSubExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message,
			bool allowPointerExpressions = false)
		{
			return ParseBinaryLeftAssociativeExpression(ParseMulDivExpression, allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions,
				(TokenType.Plus, "+"),
				(TokenType.Minus, "-"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseMulDivExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowPointerExpressions = false)
		{
			return ParseBinaryLeftAssociativeExpression(ParseUnaryExpression, allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions,
				(TokenType.Asterisk, "*"),
				(TokenType.ForwardSlash, "/"),
				(TokenType.Percent, "%"));
		}

		[Obsolete("Use ParseMulDivExpression")]
		[DebuggerStepThrough]
		private AstStatement ParseBinaryExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowPointerExpressions = false)
		{
			return ParseBinaryLeftAssociativeExpression(ParseUnaryExpression, allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions,
				(TokenType.Asterisk, "*"),
				(TokenType.ForwardSlash, "/"),
				(TokenType.Percent, "%"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseBinaryLeftAssociativeExpression(
			ExpressionParser sub, 
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowPointerExpressions, 
			params (TokenType, string)[] types)
		{
			return ParseLeftAssociativeExpression(sub, allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions, type =>
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
			MessageResolver message,
			bool allowPointerExpressions,
			Func<TokenType, string> tokenMapping)
		{
			var lhs = sub(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
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
				rhs = sub(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
				lhs = new AstBinaryExpr(op, lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
		}

		private AstStatement ParseUnaryExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message = null,
			bool allowPointerExpressions = false)
		{
			var next = PeekToken();
			if (next.Type == TokenType.Ampersand)
			{
				NextToken();
				SkipNewlines();

				var sub = ParseUnaryExpression(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
				if (sub is not AstExpression expr)
				{
					ReportMessage(sub.Location, $"Expression expected after '&'");
					return sub;
				}
				return new AstAddressOfExpr(expr, new Location(next.Location, sub.Ending));
			}
			else if (next.Type == TokenType.Asterisk)
			{
				NextToken();
				SkipNewlines();
				var sub = ParseUnaryExpression(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
				if (sub is not AstExpression expr)
				{
					ReportMessage(sub.Location, $"Expression expected after '*'");
					return sub;
				}
				return new AstPointerExpr(expr, true, new Location(next.Location, sub.Ending));
			}
			else if (next.Type == TokenType.Minus || next.Type == TokenType.Plus)
			{
				NextToken();
				SkipNewlines();
				var sub = ParseUnaryExpression(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
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
				var sub = ParseUnaryExpression(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
				return new AstUnaryExpr("!", sub, new Location(next.Location, sub.Ending));
			}

			return ParsePostUnaryExpression(allowCommaForTuple, allowFunctionDeclaration, message, allowPointerExpressions);
		}

		private AstStatement ParsePostUnaryExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowPointerExpressions = false)
		{
			var expr = ParseAtomicExpression(allowCommaForTuple, allowFunctionDeclaration, message, true, allowPointerExpressions);

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
								ReportMessage(expr.Location, $"Indentifier expected");
								return expr;
							}
							if (nestExpr.RightPart is not AstIdExpr idExpr)
							{
								ReportMessage(nestExpr.Location, $"Indentifier expected as the func name");
								return expr;
							}

							bool dotsAfter = CheckToken(TokenType.Period);

                            var callExpr = new AstCallExpr(nestExpr.LeftPart, idExpr.GetCopy(), args, new Location(expr.Beginning, end));
                            expr = new AstNestedExpr(callExpr, null, callExpr);

							// check for dots after this!!! there could be a.asd().asd().ddd().d.lll()
							// for better understand imagine we have 'anime.Asd().dwd.Lmao();'
							// and this is the first entry. So we already parsed here 'anime.Asd()' 
							// and need to check the rest shite
							if (dotsAfter)
							{
								NextToken();
								// here we are getting the rest 'dwd.Lmao'
								expr = ParseIdentifierExpression(iniNested: expr as AstNestedExpr);
								// so after this the upper loop will check if there is a OpenParent and so on
								// if there is no OpenParen - then just NestedExpr will be returned
							}
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
									ReportMessage(next.Location, $"Failed to parse operator [], expected ',' or ']'");
									//RecoverExpression();
								}
							}
							var end = Consume(TokenType.CloseBracket, ErrMsg("]", "at end of [] operator")).Location;
							if (args.Count == 0)
							{
								ReportMessage(end, "At least one argument required");
								args.Add(ParseEmptyExpression());
							}
							else if (args.Count > 1)
							{
								// TODO: mb allow them multiple args in []?
								ReportMessage(end, "Too many arguments passed");
							}

							if (args.First() is not AstExpression firstExpr)
							{
								ReportMessage(args.First().Location, $"Expression expected as index of element in [...]");
								return expr;
							}
							var arrAcc = new AstArrayAccessExpr(expr as AstExpression, firstExpr, new Location(expr.Beginning, end));
							expr = new AstNestedExpr(arrAcc, null, arrAcc);

							// check for dots after this!!! there could be a.arr[i].Length
							// for better understand imagine we have 'a.arr[i].Length;'
							// and this is the first entry. So we already parsed here 'a.arr[i]' 
							// and need to check the rest shite
							if (CheckToken(TokenType.Period))
							{
								NextToken();
								// here we are getting the rest '.Length'
								expr = ParseIdentifierExpression(iniNested: expr as AstNestedExpr);
								// so after this the upper loop will check if there is a OpenParent and so on
								// if there is no OpenParen - then just NestedExpr will be returned
							}
						}
						break;

					default:
						return expr;
				}
			}
			return expr;
		}

		private AstStatement ParseAtomicExpression(
			bool allowCommaForTuple, 
			bool allowFunctionDeclaration,
			MessageResolver message, 
			bool allowArrayExpressions = true,
			bool allowPointerExpressions = false)
		{
			var token = PeekToken();
			switch (token.Type)
			{
				case TokenType.KwBreak:
				case TokenType.KwContinue:
					NextToken();
					return new AstBreakContStmt(token.Type == TokenType.KwBreak, new Location(token.Location));

				case TokenType.KwDefault:
					{
						NextToken();
						// it is just a 'default' word
						return new AstDefaultExpr(new Location(token.Location));
					}

				case TokenType.KwNull:
					NextToken();
					// TODO: remove this shite of creating ptr to int and infer the 'null' type in post prepare!!!
					return new AstNullExpr(PointerType.GetPointerType(IntType.GetIntType(1, false)), new Location(token.Location)); 

				case TokenType.OpenBracket:
					return ParseAttributeStatement();

				case TokenType.KwNew:
					{
						return ParseNewExpression();
					}

				case TokenType.Identifier:
					{
						var id = ParseIdentifierExpression();

						// if it is a pointer or array type
						while (CheckToken(TokenType.Asterisk) || CheckToken(TokenType.ArrayDef))
						{
							if (CheckToken(TokenType.ArrayDef))
							{
								// it is not allowed usually from ParseArrayExpr
								if (!allowArrayExpressions)
									break;
								var arrExpr = new AstArrayExpr(id.RightPart, new Location(id.RightPart.Beginning, CurrentToken.Location.Ending));
								id.RightPart = arrExpr;
							}
							else
							{
								// the check is done because of some misunderstood shite
								// how to find out when 'a * b' is a mul expr
								// and 'bool* bptr' is a ptr expr?
								// so allowPointerExpressions is true only when decls are parsed!!!
								if (!allowPointerExpressions)
									break;
								var ptrExpr = new AstPointerExpr(id.RightPart, false, new Location(id.RightPart.Beginning, CurrentToken.Location.Ending));
								id.RightPart = ptrExpr;
							}
							NextToken();
						}

						// the second identifier for UnknownDecl
						if (CheckToken(TokenType.Identifier))
						{
							var name = ParseIdentifierExpression(allowDots: false);
							if (name.RightPart is not AstIdExpr idExpr)
							{
								ReportMessage(id.Location, $"Identifier expected as a name of declaration");
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
							ReportMessage(PeekToken().Location, $"Identifier expected after '~'");
							return ParseEmptyExpression();
						}

						var expr = ParseExpression(allowCommaForTuple, allowFunctionDeclaration, message);
						if (expr is AstIdExpr idExpr)
						{
							idExpr.Suffix = "~";
						}
						else
						{
							ReportMessage(PeekToken().Location, $"This type of expr was not expected after '~'");
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

				case TokenType.KwSwitch:
					return ParseSwitchStatement();
				case TokenType.KwCase:
					return ParseCaseStatement();

				case TokenType.OpenParen:
					return ParseTupleExpression(allowFunctionDeclaration, allowCommaForTuple);

				case TokenType.KwDelegate:
					return ParseDelegateDeclaration();

				case TokenType.KwStruct:
					return ParseStructDeclaration();

				case TokenType.KwEnum:
					return ParseEnumDeclaration();

				case TokenType.KwClass:
					return ParseClassDeclaration();

				// custom shite
				case TokenType.KwPublic:
				case TokenType.KwInternal:
				case TokenType.KwProtected:
				case TokenType.KwPrivate:
				case TokenType.KwUnreflected:
					return ParseAccessKeys(token.Type);

				case TokenType.KwAsync:
					return ParseSyncKeys(token.Type);

				case TokenType.KwConst:
				case TokenType.KwStatic:
					return ParseInstancingKeys(token.Type);

				case TokenType.KwAbstract:
				case TokenType.KwVirtual:
				case TokenType.KwOverride:
				case TokenType.KwPartial:
				case TokenType.KwExtern:
				case TokenType.KwSealed:
					return ParseImplementationKeys(token.Type);

				default:
					//NextToken();
					ReportMessage(token.Location, message?.Invoke(token) ?? $"Failed to parse expression, unpexpected token ({token.Type}) {token.Data}");
					return ParseEmptyExpression();
			}
		}
	}
}
