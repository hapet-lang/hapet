using HapetFrontend.Ast;
using System.Diagnostics;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		[DebuggerStepThrough]
		private (TokenLocation beg, TokenLocation end) GetWhitespaceLocation()
		{
			var end = _lexer.PeekToken().Location;
			return (new TokenLocation
			{
				File = end.File,
				Index = _lastNonWhitespace?.Location?.End ?? 0,
				End = _lastNonWhitespace?.Location?.End ?? 0,
				Line = _lastNonWhitespace?.Location?.Line ?? end.Line,
				LineStartIndex = _lastNonWhitespace?.Location?.LineStartIndex ?? end.LineStartIndex,
			}, end);
		}

		[SkipInStackFrame]
		[DebuggerStepThrough]
		public void SkipNewlines()
		{
			while (true)
			{
				var tok = _lexer.PeekToken();

				switch (tok.Type)
				{
					case TokenType.EOF:
						return;
					case TokenType.DocComment:
						this.AppendDocString(tok.Data as string);
						NextToken();
						break;
					case TokenType.NewLine:
						NextToken();
						break;
					default:
						return;
				}
			}
		}

		[SkipInStackFrame]
		[DebuggerStepThrough]
		public Token NextToken()
		{
			_currentToken = _lexer.NextToken();
			if (_currentToken.Type != TokenType.NewLine)
				_lastNonWhitespace = _currentToken;
			return _currentToken;
		}

		[SkipInStackFrame]
		[DebuggerStepThrough]
		public bool Expect(TokenType type, MessageResolver customErrorMessage, bool skipNewLine = false)
		{
			var tok = PeekToken();
			while (skipNewLine && tok.Type == TokenType.NewLine)
			{
				NextToken();
				tok = PeekToken();
			}

			if (tok.Type != type)
			{
				string message = customErrorMessage?.Invoke(tok) ?? $"Unexpected Token ({tok.Type}) {tok.Data}, expected {type}";
				ReportError(tok.Location, message);
				return false;
			}

			NextToken();
			return true;
		}

		[SkipInStackFrame]
		[DebuggerStepThrough]
		public Token Consume(TokenType type, MessageResolver customMessage, bool skipNewLine = false)
		{
			if (!Expect(type, customMessage, skipNewLine))
				NextToken();
			return CurrentToken;
		}

		[SkipInStackFrame]
		[DebuggerStepThrough]
		public Token ConsumeUntil(TokenType type, MessageResolver customMessage, bool skipNewLine = false)
		{
			var tok = PeekToken();
			while (tok.Type != type)
			{
				if (!skipNewLine || tok.Type != TokenType.NewLine)
					ReportError(tok.Location, customMessage?.Invoke(tok));

				NextToken();
				tok = PeekToken();

				if (tok.Type == TokenType.EOF)
					break;
			}

			if (!Expect(type, customMessage))
				NextToken();
			return CurrentToken;
		}

		[DebuggerStepThrough]
		public bool CheckToken(TokenType type)
		{
			var next = PeekToken();
			return next.Type == type;
		}

		[DebuggerStepThrough]
		public bool CheckTokens(params TokenType[] types)
		{
			var next = PeekToken();
			foreach (var (t, i) in types.Select((t, i) => (t, i)))
			{
				if (next.Type == t)
				{
					return true;
				}
			}
			return false;
		}

		[DebuggerStepThrough]
		public Token PeekToken()
		{
			return _lexer.PeekToken();
		}

		// TODO: do i need it?
		public bool IsTypeExprToken()
		{
			var next = PeekToken();
			switch (next.Type)
			{
				case TokenType.OpenParen:
				case TokenType.OpenBracket:
				case TokenType.Ampersand:
				case TokenType.Hat:
				case TokenType.Identifier:
				case TokenType.DollarIdentifier:
					return true;

				default:
					return false;
			}
		}

		// TODO: do i need it?
		public bool IsExprToken(params TokenType[] exclude)
		{
			var next = PeekToken();
			if (exclude.Contains(next.Type))
				return false;
			switch (next.Type)
			{
				case TokenType.Plus:
				case TokenType.Minus:
				case TokenType.LessLess:
				case TokenType.OpenParen:
				case TokenType.OpenBracket:
				case TokenType.OpenBrace:
				case TokenType.StringLiteral:
				case TokenType.CharLiteral:
				case TokenType.NumberLiteral:
				case TokenType.KwNull:
				case TokenType.KwTrue:
				case TokenType.KwFalse:
				case TokenType.KwSwitch:
				case TokenType.KwIf:
				case TokenType.Ampersand:
				case TokenType.Hat:
				case TokenType.Asterisk:
				case TokenType.Bang:
				case TokenType.Identifier:
				case TokenType.AtSignIdentifier:
				case TokenType.DollarIdentifier:
				case TokenType.PeriodPeriod:
				case TokenType.Period:
					return true;

				default:
					return false;
			}
		}

		private void RecoverStatement()
		{
			while (true)
			{
				var next = PeekToken();
				switch (next.Type)
				{
					case TokenType.NewLine:
						NextToken();
						return;

					case TokenType.CloseBrace:
						return;

					case TokenType.EOF:
						return;

					default:
						NextToken();
						break;
				}
			}
		}
	}
}
