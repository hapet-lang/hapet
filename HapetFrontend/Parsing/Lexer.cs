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
                case '>' when Next(location) == '>': SimpleToken(location, ref token, TokenType.GreaterGreater, 2); break;
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

        private static void CheckKeywords(ref Token token)
        {
            switch (token.Data as string)
            {
                case "interface": token.Type = TokenType.KwInterface; break;
                case "class": token.Type = TokenType.KwClass; break;
                case "struct": token.Type = TokenType.KwStruct; break;
                case "enum": token.Type = TokenType.KwEnum; break;
                case "delegate": token.Type = TokenType.KwDelegate; break;

                case "true": token.Type = TokenType.KwTrue; break;
                case "false": token.Type = TokenType.KwFalse; break;
                case "null": token.Type = TokenType.KwNull; break;

                case "using": token.Type = TokenType.KwUsing; break;
                case "namespace": token.Type = TokenType.KwNamespace; break;

                case "if": token.Type = TokenType.KwIf; break;
                case "else": token.Type = TokenType.KwElse; break;
                case "switch": token.Type = TokenType.KwSwitch; break;
                case "case": token.Type = TokenType.KwCase; break;
                case "for": token.Type = TokenType.KwFor; break;
                case "foreach": token.Type = TokenType.KwForeach; break;
                case "while": token.Type = TokenType.KwWhile; break;
                case "do": token.Type = TokenType.KwDo; break;
                case "goto": token.Type = TokenType.KwGoto; break;

                // exceptions
                case "try": token.Type = TokenType.KwTry; break;
                case "catch": token.Type = TokenType.KwCatch; break;
                case "finally": token.Type = TokenType.KwFinally; break;
                case "throw": token.Type = TokenType.KwThrow; break;

                case "break": token.Type = TokenType.KwBreak; break;
                case "continue": token.Type = TokenType.KwContinue; break;
                case "return": token.Type = TokenType.KwReturn; break;
                case "yield": token.Type = TokenType.KwYield; break;

                case "const": token.Type = TokenType.KwConst; break;
                case "readonly": token.Type = TokenType.KwReadonly; break;
                case "unsafe": token.Type = TokenType.KwUnsafe; break;
                case "volatile": token.Type = TokenType.KwVolatile; break;
                case "global": token.Type = TokenType.KwGlobal; break;
                case "default": token.Type = TokenType.KwDefault; break;
                case "new": token.Type = TokenType.KwNew; break;
                case "base": token.Type = TokenType.KwBase; break;
                case "sizeof": token.Type = TokenType.KwSizeof; break;
                case "alignof": token.Type = TokenType.KwAlignof; break;
                case "typeof": token.Type = TokenType.KwTypeof; break;
                case "nameof": token.Type = TokenType.KwNameof; break;

                case "get": token.Type = TokenType.KwGet; break;
                case "set": token.Type = TokenType.KwSet; break;

                case "in": token.Type = TokenType.KwIn; break;
                case "is": token.Type = TokenType.KwIs; break;
                case "as": token.Type = TokenType.KwAs; break;
                case "ref": token.Type = TokenType.KwRef; break;
                case "out": token.Type = TokenType.KwOut; break;
                case "params": token.Type = TokenType.KwParams; break;

                case "public": token.Type = TokenType.KwPublic; break;
                case "internal": token.Type = TokenType.KwInternal; break;
                case "protected": token.Type = TokenType.KwProtected; break;
                case "private": token.Type = TokenType.KwPrivate; break;
                case "unreflected": token.Type = TokenType.KwUnreflected; break;

                case "async": token.Type = TokenType.KwAsync; break;
                case "await": token.Type = TokenType.KwAwait; break;

                case "static": token.Type = TokenType.KwStatic; break;
                case "abstract": token.Type = TokenType.KwAbstract; break;
                case "virtual": token.Type = TokenType.KwVirtual; break;
                case "override": token.Type = TokenType.KwOverride; break;
                case "partial": token.Type = TokenType.KwPartial; break;
                case "extern": token.Type = TokenType.KwExtern; break;
                case "sealed": token.Type = TokenType.KwSealed; break;

                // for events
                case "event": token.Type = TokenType.KwEvent; break;
                case "add": token.Type = TokenType.KwAdd; break;
                case "remove": token.Type = TokenType.KwRemove; break;

                // for overriding casts
                case "explicit": token.Type = TokenType.KwExplicit; break;
                case "implicit": token.Type = TokenType.KwImplicit; break;
                // for overriding operators
                case "operator": token.Type = TokenType.KwOperator; break;
            }
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
