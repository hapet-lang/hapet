using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using System.Text;

namespace HapetFrontend.Parsing
{
    public interface ILexer
    {
        string Text { get; }
        Token PeekToken();
        Token NextToken();

        void UpdateLookAheadLocation();
        Token NextLookAhead(bool skipWhitespaces);
        Token PeekLookAhead(bool skipWhitespaces);
    }

    public partial class Lexer : ILexer
    {
        private string _text;
        private TokenLocation _location;
        private TokenLocation _lookAheadLocation;

        private char Current(TokenLocation location) => location.Index < _text.Length ? _text[location.Index] : (char)0;
        private char Next(TokenLocation location) => location.Index < _text.Length - 1 ? _text[location.Index + 1] : (char)0;
        private char GetChar(int offset, TokenLocation location) => location.Index + offset < _text.Length ? _text[location.Index + offset] : (char)0;
        private Token _peek = null;
        private Token _peekLookAhead = null;

        public string Text => _text;
        private IMessageHandler _messageHandler;

        public static Lexer FromFile(string fileName, IMessageHandler messageHandler)
        {
            if (!File.Exists(fileName))
            {
                messageHandler.ReportMessage([fileName], ErrorCode.Get(CTEN.FileForLexerNotFound));
                return null;
            }
            return new Lexer
            {
                _messageHandler = messageHandler,
                _text = File.ReadAllText(fileName, Encoding.UTF8)
                    .Replace("\r\n", "\n", StringComparison.InvariantCulture)
                    .Replace("\t", "    ", StringComparison.InvariantCulture),
                _location = new TokenLocation
                {
                    File = fileName,
                    Line = 1,
                    Index = 0,
                    LineStartIndex = 0
                },
                _lookAheadLocation = new TokenLocation
                {
                    File = fileName,
                    Line = 1,
                    Index = 0,
                    LineStartIndex = 0
                },
            };
        }

        public static Lexer FromString(string str, IMessageHandler messageHandler, string fileName = "string")
        {
            return new Lexer
            {
                _messageHandler = messageHandler,
                _text = str.Replace("\r\n", "\n", StringComparison.InvariantCulture),
                _location = new TokenLocation
                {
                    File = fileName,
                    Line = 1,
                },
                _lookAheadLocation = new TokenLocation
                {
                    File = fileName,
                    Line = 1,
                },
            };
        }

        public Token PeekToken()
        {
            if (_peek == null)
            {
                _peek = NextToken();
            }
            return _peek;
        }

        public Token PeekLookAhead(bool skipWhitespaces = true)
        {
            if (_peekLookAhead == null)
            {
                _peekLookAhead = NextLookAhead(skipWhitespaces);
            }
            return _peekLookAhead;
        }

        public Token NextToken()
        {
            if (_peek != null)
            {
                Token t = _peek;
                _peek = null;
                return t;
            }

            if (SkipWhitespaceAndComments(_location, out TokenLocation loc))
            {
                loc.End = loc.Index;
                Token tok = new Token();
                tok.Location = loc;
                tok.Type = TokenType.NewLine;
                return tok;
            }

            return ReadToken(_location);
        }

        public Token NextLookAhead(bool skipWhitespaces = true)
        {
            if (_peekLookAhead != null)
            {
                Token t = _peekLookAhead;
                _peekLookAhead = null;
                return t;
            }

            if (SkipWhitespaceAndComments(_lookAheadLocation, out TokenLocation loc, skipWhitespaces))
            {
                loc.End = loc.Index;
                Token tok = new Token();
                tok.Location = loc;
                tok.Type = TokenType.NewLine;
                return tok;
            }

            return ReadToken(_lookAheadLocation);
        }

        public void UpdateLookAheadLocation()
        {
            _lookAheadLocation.Index = _location.Index;
            _lookAheadLocation.Line = _location.Line;
            _lookAheadLocation.LineStartIndex = _location.LineStartIndex;
            _lookAheadLocation.End = _location.End;
            _peekLookAhead = _peek;
        }


        private StringBuilder tokenDataBuilder = new StringBuilder();
        private Token ReadToken(TokenLocation location)
        {
            var token = new Token();
            token.Location = location.Clone();
            token.Location.End = token.Location.Index;
            token.Type = TokenType.EOF;
            if (location.Index >= _text.Length)
                return token;

            tokenDataBuilder.Clear();

            switch (Current(location))
            {
                case '+' when Next(location) == '+': SimpleToken(location, ref token, TokenType.PlusPlus, 2); break;
                case '-' when Next(location) == '-': SimpleToken(location, ref token, TokenType.MinusMinus, 2); break;
                case '=' when Next(location) == '>': SimpleToken(location, ref token, TokenType.Arrow, 2); break;
                case '=' when Next(location) == '=': SimpleToken(location, ref token, TokenType.DoubleEqual, 2); break;
                case '!' when Next(location) == '=': SimpleToken(location, ref token, TokenType.NotEqual, 2); break;
                case '<' when Next(location) == '=': SimpleToken(location, ref token, TokenType.LessEqual, 2); break;
                case '<' when Next(location) == '<': SimpleToken(location, ref token, TokenType.LessLess, 2); break;
                // https://stackoverflow.com/questions/13428934/is-c-sharp-considered-a-context-free-language
                //case '>' when Next(location) == '>': SimpleToken(location, ref token, TokenType.GreaterGreater, 2); break;
                case '>' when Next(location) == '=': SimpleToken(location, ref token, TokenType.GreaterEqual, 2); break;
                case '+' when Next(location) == '=': SimpleToken(location, ref token, TokenType.AddEq, 2); break;
                case '-' when Next(location) == '=': SimpleToken(location, ref token, TokenType.SubEq, 2); break;
                case '*' when Next(location) == '=': SimpleToken(location, ref token, TokenType.MulEq, 2); break;
                case '/' when Next(location) == '=': SimpleToken(location, ref token, TokenType.DivEq, 2); break;
                case '%' when Next(location) == '=': SimpleToken(location, ref token, TokenType.ModEq, 2); break;
                case '.' when Next(location) == '.': SimpleToken(location, ref token, TokenType.PeriodPeriod, 2); break;
                case '&' when Next(location) == '&': SimpleToken(location, ref token, TokenType.LogicalAnd, 2); break;
                case '[' when Next(location) == ']': SimpleToken(location, ref token, TokenType.ArrayDef, 2); break;
                case '|' when Next(location) == '|': SimpleToken(location, ref token, TokenType.LogicalOr, 2); break;
                case '/' when (Next(location) == '/' && GetChar(2, location) == '/'):
                    {
                        // doc comment

                        if (GetChar(3, location) == ' ')
                        {
                            token.Type = TokenType.DocComment;
                            int index = 0;
                            while (location.Index < _text.Length && Current(location) != '\n')
                            {
                                if (index >= 4)
                                    tokenDataBuilder.Append(Current(location));
                                location.Index += 1;
                                index += 1;
                            }
                            if (location.Index < _text.Length && Current(location) == '\n')
                            {
                                location.Index += 1;
                                location.Line++;
                                location.LineStartIndex = location.Index;
                            }
                            token.Data = tokenDataBuilder.ToString();
                        }
                        else if (GetChar(3, location) == '*')
                        {
                            token.Type = TokenType.DocComment;
                            token.Data = ParseMultiLineDocComment(location);
                        }
                        else
                        {
                            throw new Exception("this shouldn't happen");
                        }

                        break;
                    }
                case '~': SimpleToken(location, ref token, TokenType.Tilda); break;
                case ':': SimpleToken(location, ref token, TokenType.Colon); break;
                case ';': SimpleToken(location, ref token, TokenType.Semicolon); break;
                case '.': SimpleToken(location, ref token, TokenType.Period); break;
                case '=': SimpleToken(location, ref token, TokenType.Equal); break;
                case '(': SimpleToken(location, ref token, TokenType.OpenParen); break;
                case ')': SimpleToken(location, ref token, TokenType.CloseParen); break;
                case '{': SimpleToken(location, ref token, TokenType.OpenBrace); break;
                case '}': SimpleToken(location, ref token, TokenType.CloseBrace); break;
                case '[': SimpleToken(location, ref token, TokenType.OpenBracket); break;
                case ']': SimpleToken(location, ref token, TokenType.CloseBracket); break;
                case ',': SimpleToken(location, ref token, TokenType.Comma); break;
                case '&': SimpleToken(location, ref token, TokenType.Ampersand); break;
                case '^': SimpleToken(location, ref token, TokenType.Hat); break;
                case '*': SimpleToken(location, ref token, TokenType.Asterisk); break;
                case '/': SimpleToken(location, ref token, TokenType.ForwardSlash); break;
                case '+': SimpleToken(location, ref token, TokenType.Plus); break;
                case '%': SimpleToken(location, ref token, TokenType.Percent); break;
                case '-': SimpleToken(location, ref token, TokenType.Minus); break;
                case '<': SimpleToken(location, ref token, TokenType.Less); break;
                case '>': SimpleToken(location, ref token, TokenType.Greater); break;
                case '!': SimpleToken(location, ref token, TokenType.Bang); break;
                case '|': SimpleToken(location, ref token, TokenType.VerticalSlash); break;

                case '"': ParseStringLiteral(location, ref token, '"'); break;
                case '\'':
                    {
                        ParseStringLiteral(location, ref token, '\'');
                        token.Type = TokenType.CharLiteral;
                        break;
                    }

                case char cc when IsDigit(cc):
                    ParseNumberLiteral(location, ref token);
                    break;

                case '$': ParseIdentifier(location, ref token, TokenType.DollarIdentifier); break;
                case '#': ParseIdentifier(location, ref token, TokenType.SharpIdentifier); break;
                case '@': ParseIdentifier(location, ref token, TokenType.AtSignIdentifier); break;

                case char cc when IsIdentBegin(cc):
                    ParseIdentifier(location, ref token, TokenType.Identifier);
                    CheckKeywords(ref token);
                    break;

                default:
                    token.Type = TokenType.Unknown;
                    location.Index += 1;
                    break;
            }

            if (token.Type == TokenType.StringLiteral || token.Type == TokenType.NumberLiteral || token.Type == TokenType.CharLiteral)
            {
                if (IsIdentBegin(Current(location)))
                {
                    token.Suffix = "" + Current(location);
                    location.Index++;

                    while (IsIdent(Current(location)))
                    {
                        token.Suffix += Current(location);
                        location.Index++;
                    }
                }
            }

            token.Location.End = location.Index;
            return token;
        }

        private void SimpleToken(TokenLocation location, ref Token token, TokenType type, int len = 1)
        {
            token.Type = type;
            location.Index += len;
        }

        private static bool IsBinaryDigit(char c)
        {
            return c == '0' || c == '1';
        }

        private static bool IsHexDigit(char c)
        {
            return IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private static bool IsIdentBegin(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
        }

        private static bool IsIdent(char c)
        {
            return IsIdentBegin(c) || (c >= '0' && c <= '9');
        }
    }
}
