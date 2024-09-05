using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		public AstStatement ParseExpression(bool allowCommaForTuple, bool allowFunctionExpression = false, ErrorMessageResolver errorMessage = null)
		{
			errorMessage = errorMessage ?? (t => $"Unexpected token '{t}' in expression");

			var expr = ParseIsExpression(false, allowFunctionExpression, errorMessage);

			return expr;
		}

		private AstStatement ParseIsExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage)
		{
			var lhs = ParseAsExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
			AstStatement rhs = null;

			while (CheckToken(TokenType.KwIs))
			{
				var _is = NextToken();
				SkipNewlines();
				rhs = ParseAsExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
				lhs = new AstBinaryExpr("is", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
			return lhs;
		}

		private AstStatement ParseAsExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage)
		{
			var lhs = ParseInExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
			AstStatement rhs = null;

			while (CheckToken(TokenType.KwAs))
			{
				var _as = NextToken();
				SkipNewlines();
				rhs = ParseInExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
				// lhs = new AstBinaryExpr("as", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
				// TODO: do i really have to use Cast?
				lhs = new AstCastExpr(rhs, lhs, new Location(lhs.Beginning, rhs.Ending));
			}
			return lhs;
		}

		private AstStatement ParseInExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage)
		{
			var lhs = ParseOrExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
			AstStatement rhs = null;

			while (CheckToken(TokenType.KwIn))
			{
				var _in = NextToken();
				SkipNewlines();
				rhs = ParseOrExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);
				lhs = new AstBinaryExpr("in", lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
			return lhs;
		}

		[DebuggerStepThrough]
		private AstStatement ParseOrExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseAndExpression, allowCommaForTuple, allowFunctionExpression, e,
				(TokenType.LogicalOr, "||"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseAndExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseComparisonExpression, allowCommaForTuple, allowFunctionExpression, e,
				(TokenType.LogicalAnd, "&&"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseComparisonExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseAddSubExpression, allowCommaForTuple, allowFunctionExpression, e,
				(TokenType.Less, "<"),
				(TokenType.LessEqual, "<="),
				(TokenType.Greater, ">"),
				(TokenType.GreaterEqual, ">="),
				(TokenType.DoubleEqual, "=="),
				(TokenType.NotEqual, "!="));
		}

		[DebuggerStepThrough]
		private AstStatement ParseAddSubExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseMulDivExpression, allowCommaForTuple, allowFunctionExpression, e,
				(TokenType.Plus, "+"),
				(TokenType.Minus, "-"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseMulDivExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseUnaryExpression, allowCommaForTuple, allowFunctionExpression, e,
				(TokenType.Asterisk, "*"),
				(TokenType.ForwardSlash, "/"),
				(TokenType.Percent, "%"));
		}

		[Obsolete("Use ParseMulDivExpression")]
		[DebuggerStepThrough]
		private AstStatement ParseBinaryExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e)
		{
			return ParseBinaryLeftAssociativeExpression(ParseUnaryExpression, allowCommaForTuple, allowFunctionExpression, e,
				(TokenType.Asterisk, "*"),
				(TokenType.ForwardSlash, "/"),
				(TokenType.Percent, "%"));
		}

		[DebuggerStepThrough]
		private AstStatement ParseBinaryLeftAssociativeExpression(ExpressionParser sub, bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage, params (TokenType, string)[] types)
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

		private AstStatement ParseLeftAssociativeExpression(
			ExpressionParser sub,
			bool allowCommaForTuple,
			bool allowFunctionExpression,
			ErrorMessageResolver errorMessage,
			Func<TokenType, string> tokenMapping)
		{
			var lhs = sub(allowCommaForTuple, allowFunctionExpression, errorMessage);
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
				rhs = sub(allowCommaForTuple, allowFunctionExpression, errorMessage);
				lhs = new AstBinaryExpr(op, lhs, rhs, new Location(lhs.Beginning, rhs.Ending));
			}
		}

		private AstStatement ParseUnaryExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage = null)
		{
			var next = PeekToken();
			if (next.Type == TokenType.Hat)
			{
				NextToken();
				SkipNewlines();

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

		private AstStatement ParsePostUnaryExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage)
		{
			var expr = ParseAtomicExpression(allowCommaForTuple, allowFunctionExpression, errorMessage);

			while (true)
			{
				switch (PeekToken().Type)
				{
					// TODO: uncomment and check
					//case TokenType.OpenParen:
					//	{
					//		NextToken();
					//		SkipNewlines();
					//		var args = new List<AstArgExpr>();
					//		while (true)
					//		{
					//			var next = PeekToken();
					//			if (next.Type == TokenType.CloseParen || next.Type == TokenType.EOF)
					//				break;
					//			args.Add(ParseArgumentExpression());

					//			next = PeekToken();
					//			if (next.Type == TokenType.NewLine)
					//			{
					//				NextToken();
					//			}
					//			else if (next.Type == TokenType.Comma)
					//			{
					//				NextToken();
					//				SkipNewlines();
					//			}
					//			else if (next.Type == TokenType.CloseParen)
					//				break;
					//			else
					//			{
					//				NextToken();
					//				ReportError(next.Location, $"Failed to parse function call, expected ',' or ')'");
					//				//RecoverExpression();
					//			}
					//		}
					//		var end = Consume(TokenType.CloseParen, ErrMsg(")", "at end of function call")).Location;
					//		expr = new AstCallExpr(expr, args, new Location(expr.Beginning, end));
					//	}
					//	break;

					// TODO: uncomment and check
					//case TokenType.OpenBracket:
					//	{
					//		NextToken();
					//		SkipNewlines();

					//		var args = new List<AstStatement>();
					//		while (true)
					//		{
					//			var next = PeekToken();
					//			if (next.Type == TokenType.CloseBracket || next.Type == TokenType.EOF)
					//				break;
					//			args.Add(ParseExpression(false));
					//			SkipNewlines();

					//			next = PeekToken();
					//			if (next.Type == TokenType.Comma)
					//			{
					//				NextToken();
					//				SkipNewlines();
					//			}
					//			else if (next.Type == TokenType.CloseBracket)
					//				break;
					//			else
					//			{
					//				NextToken();
					//				ReportError(next.Location, $"Failed to parse operator [], expected ',' or ']'");
					//				//RecoverExpression();
					//			}
					//		}
					//		var end = Consume(TokenType.CloseBracket, ErrMsg("]", "at end of [] operator")).Location;
					//		if (args.Count == 0)
					//		{
					//			ReportError(end, "At least one argument required");
					//			args.Add(ParseEmptyExpression());
					//		}
					//		expr = new AstArrayAccessExpr(expr, args, new Location(expr.Beginning, end));
					//	}
					//	break;

					// TODO: should be parsed as one AstIdExpr
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

		private AstStatement ParseAtomicExpression(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver errorMessage)
		{
			var token = PeekToken();
			switch (token.Type)
			{
				case TokenType.KwBreak:
					return ParseBreakStatement();

				case TokenType.KwContinue:
					return ParseContinueStatement();

				case TokenType.KwDefault:
					NextToken();
					return new AstDefaultExpr(new Location(token.Location));

				case TokenType.KwNull:
					NextToken();
					return new AstNullExpr(new Location(token.Location));

					// TODO: what should i do here
				//case TokenType.OpenBracket:
				//	return ParseArrayOrSliceExpression();

				case TokenType.Identifier:
					{
						NextToken();
						var id = new AstIdExpr((string)token.Data, new Location(token.Location));
						return id;
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
					return ParseBlockStatement();

				case TokenType.KwIf:
					return ParseConditionExpression(allowCommaForTuple);

				case TokenType.KwSwitch:
					return ParseSwitchExpression();

				case TokenType.OpenParen:
					return ParseTupleExpression(allowFunctionExpression, allowCommaForTuple);

				case TokenType.Ampersand:
					NextToken();
					SkipNewlines();

					var target = ParseExpression(allowCommaForTuple);
					return new AstReferenceTypeExpr(target, new Location(token.Location, target.Ending));

				case TokenType.KwStruct:
					return ParseStructDeclaration();

				case TokenType.KwEnum:
					return ParseEnumDeclaration();

				case TokenType.KwClass:
					return ParseClassDeclaration();

				default:
					//NextToken();
					ReportError(token.Location, errorMessage?.Invoke(token) ?? $"Failed to parse expression, unpexpected token ({token.Type}) {token.Data}");
					return ParseEmptyExpression();
			}
		}
	}
}
