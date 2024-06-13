using Frontend.Errors;
using Frontend.Parsing.Entities;
using System.Diagnostics;
using System.Text;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		public delegate string ErrorMessageResolver(Token t);

		private ILexer _lexer;
		private IErrorHandler _errorHandler;

		private Token _lastNonWhitespace = null;

		private Token _currentToken = null;
		private Token CurrentToken => _currentToken; // probably could be public

		private StringBuilder _docString = new StringBuilder();

		public Parser(ILexer lex, IErrorHandler errHandler)
		{
			_lexer = lex;
			_errorHandler = errHandler;
		}

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
		public bool Expect(TokenType type, ErrorMessageResolver customErrorMessage, bool skipNewLine = false)
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
		public Token Consume(TokenType type, ErrorMessageResolver customErrorMessage, bool skipNewLine = false)
		{
			if (!Expect(type, customErrorMessage, skipNewLine))
				NextToken();
			return CurrentToken;
		}

		[SkipInStackFrame]
		[DebuggerStepThrough]
		public Token ConsumeUntil(TokenType type, ErrorMessageResolver customErrorMessage, bool skipNewLine = false)
		{
			var tok = PeekToken();
			while (tok.Type != type)
			{
				if (!skipNewLine || tok.Type != TokenType.NewLine)
					ReportError(tok.Location, customErrorMessage?.Invoke(tok));

				NextToken();
				tok = PeekToken();

				if (tok.Type == TokenType.EOF)
					break;
			}

			if (!Expect(type, customErrorMessage))
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
			foreach (var t in types)
			{
				if (next.Type == t)
					return true;
			}
			return false;
		}

		[DebuggerStepThrough]
		public Token PeekToken()
		{
			return _lexer.PeekToken();
		}

		#region Errors
		[DebuggerStepThrough]
		public void ReportError(TokenLocation Location, string message)
		{
			var (callingFunctionName, callingFunctionFile, callLineNumber) = Funcad.GetCallingFunction().GetValueOrDefault(("", "", -1));
			_errorHandler.ReportError(_lexer.Text, new Location(Location), message, null, callingFunctionFile, callingFunctionName, callLineNumber);
		}

		[DebuggerStepThrough]
		public void ReportError(ILocation Location, string message)
		{
			var (callingFunctionName, callingFunctionFile, callLineNumber) = Funcad.GetCallingFunction().GetValueOrDefault(("", "", -1));
			_errorHandler.ReportError(_lexer.Text, Location, message, null, callingFunctionFile, callingFunctionName, callLineNumber);
		}

		[DebuggerStepThrough]
		public static ErrorMessageResolver ErrMsg(string expect, string where = null)
		{
			return t => $"Expected {expect} {where}";
		}

		[DebuggerStepThrough]
		private static ErrorMessageResolver ErrMsgUnexpected(string expect, string where = null)
		{
			return t => $"Unexpected token {t} at {where}. Expected {expect}";
		}
		#endregion

		#region Other
		private string GetCurrentDocString()
		{
			string doc = _docString.ToString();
			_docString.Clear();
			return doc;
		}

		private void AppendDocString(string value)
		{
			_docString.AppendLine(value);
		}
		#endregion
	}
}
