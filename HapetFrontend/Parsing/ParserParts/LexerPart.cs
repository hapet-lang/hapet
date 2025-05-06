using HapetFrontend.Ast;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
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
        public void UpdateLookAheadLocation()
        {
            _lexer.UpdateLookAheadLocation();
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
        public Token NextLookAhead(bool skipWhitespaces = true)
        {
            var token = _lexer.NextLookAhead(skipWhitespaces);
            return token;
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
                customErrorMessage ??= new MessageResolver() 
                { 
                    MessageArgs = [tok.Type.ToString(), tok.Data.ToString(), type.ToString()], 
                    XmlMessage = ErrorCode.Get(CTEN.CommonUnexpectedToken) 
                };
                ReportMessage(tok.Location, customErrorMessage.MessageArgs, customErrorMessage.XmlMessage);
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
                    ReportMessage(tok.Location, customMessage == null ? [] : customMessage.MessageArgs, customMessage?.XmlMessage);

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
        public bool CheckLookAhead(TokenType type, bool skipWhitespaces = true)
        {
            var next = _lexer.PeekLookAhead(skipWhitespaces);
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

        [DebuggerStepThrough]
        public Token PeekLookAhead(bool skipWhitespaces = true)
        {
            return _lexer.PeekLookAhead(skipWhitespaces);
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
