using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using System.Diagnostics;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        [DebuggerStepThrough]
        private (TokenLocation beg, TokenLocation end) GetWhitespaceLocation(ParserInInfo inInfo)
        {
            var end = PeekToken(inInfo).Location;
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
        public void SkipNewlines(ParserInInfo inInfo)
        {
            while (true)
            {
                var tok = PeekToken(inInfo);

                switch (tok.Type)
                {
                    case TokenType.EOF:
                        return;
                    case TokenType.DocComment:
                        if (!inInfo.IsLookAheadParsing)
                            AppendDocString(tok.Data as string);
                        NextToken(inInfo);
                        break;
                    case TokenType.NewLine:
                        NextToken(inInfo);
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
        public void SaveLookAheadLocation()
        {
            _lexer.SaveLookAheadLocation();
        }

        [SkipInStackFrame]
        [DebuggerStepThrough]
        public void RestoreLookAheadLocation()
        {
            _lexer.RestoreLookAheadLocation();
        }

        [SkipInStackFrame]
        [DebuggerStepThrough]
        public Token NextToken(ParserInInfo inInfo)
        {
            if (inInfo.IsLookAheadParsing)
                return _lexer.NextLookAhead(true);

            _currentToken = _lexer.NextToken();
            if (_currentToken.Type != TokenType.NewLine)
                _lastNonWhitespace = _currentToken;
            return _currentToken;
        }

        [SkipInStackFrame]
        [DebuggerStepThrough]
        public void SetLocation(TokenLocation newLocation)
        {
            _lexer.SetLocation(newLocation);
        }

        [SkipInStackFrame]
        [DebuggerStepThrough]
        public bool Expect(ParserInInfo inInfo, TokenType type, MessageResolver customErrorMessage, bool skipNewLine = false)
        {
            var tok = PeekToken(inInfo);
            while (skipNewLine && tok.Type == TokenType.NewLine)
            {
                NextToken(inInfo);
                tok = PeekToken(inInfo);
            }

            if ((tok.Type != type) && !inInfo.IsLookAheadParsing)
            {
                customErrorMessage ??= new MessageResolver() 
                { 
                    MessageArgs = [tok.Type.ToString(), tok.Data.ToString(), type.ToString()], 
                    XmlMessage = ErrorCode.Get(CTEN.CommonUnexpectedToken) 
                };
                ReportMessage(tok.Location, customErrorMessage.MessageArgs, customErrorMessage.XmlMessage);
                return false;
            }

            NextToken(inInfo);
            return true;
        }

        [SkipInStackFrame]
        [DebuggerStepThrough]
        public Token Consume(ParserInInfo inInfo, TokenType type, MessageResolver customMessage, bool skipNewLine = false)
        {
            if (!Expect(inInfo, type, customMessage, skipNewLine))
                NextToken(inInfo);
            return CurrentToken;
        }

        [SkipInStackFrame]
        [DebuggerStepThrough]
        public Token ConsumeUntil(ParserInInfo inInfo, TokenType type, MessageResolver customMessage, bool skipNewLine = false)
        {
            var tok = PeekToken(inInfo);
            while (tok.Type != type)
            {
                if ((!skipNewLine || tok.Type != TokenType.NewLine) && !inInfo.IsLookAheadParsing)
                    ReportMessage(tok.Location, customMessage == null ? [] : customMessage.MessageArgs, customMessage?.XmlMessage);

                NextToken(inInfo);
                tok = PeekToken(inInfo);

                if (tok.Type == TokenType.EOF)
                    break;
            }

            if (!Expect(inInfo, type, customMessage))
                NextToken(inInfo);
            return CurrentToken;
        }

        [DebuggerStepThrough]
        public bool CheckToken(ParserInInfo inInfo, TokenType type)
        {
            var next = PeekToken(inInfo);
            return next.Type == type;
        }

        [DebuggerStepThrough]
        public bool CheckTokens(ParserInInfo inInfo, params TokenType[] types)
        {
            var next = PeekToken(inInfo);
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
        public Token PeekToken(ParserInInfo inInfo)
        {
            if (inInfo.IsLookAheadParsing)
                return _lexer.PeekLookAhead(true);
            return _lexer.PeekToken();
        }

        private void RecoverStatement(ParserInInfo inInfo)
        {
            while (true)
            {
                var next = PeekToken(inInfo);
                switch (next.Type)
                {
                    case TokenType.NewLine:
                        NextToken(inInfo);
                        return;

                    case TokenType.CloseBrace:
                        return;

                    case TokenType.EOF:
                        return;

                    default:
                        NextToken(inInfo);
                        break;
                }
            }
        }
    }
}
