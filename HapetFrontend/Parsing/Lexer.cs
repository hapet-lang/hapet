using HapetFrontend.Ast;
using HapetFrontend.Entities;
using System.Text;

namespace HapetFrontend.Parsing
{
	public interface ILexer
	{
		string Text { get; }
		Token PeekToken();
		Token NextToken();
	}

	public partial class Lexer : ILexer
	{
		private string _text;
		private TokenLocation _location;

		private char Current => _location.Index < _text.Length ? _text[_location.Index] : (char)0;
		private char Next => _location.Index < _text.Length - 1 ? _text[_location.Index + 1] : (char)0;
		private char GetChar(int offset) => _location.Index + offset < _text.Length ? _text[_location.Index + offset] : (char)0;
		private Token peek = null;

		public string Text => _text;
		private IErrorHandler _errorHandler;

		public static Lexer FromFile(string fileName, IErrorHandler errorHandler)
		{
			if (!File.Exists(fileName))
			{
				errorHandler.ReportError($"'{fileName}' could not be found.");
				return null;
			}
			return new Lexer
			{
				_errorHandler = errorHandler,
				_text = File.ReadAllText(fileName, Encoding.UTF8)
					.Replace("\r\n", "\n", StringComparison.InvariantCulture)
					.Replace("\t", "    ", StringComparison.InvariantCulture),
				_location = new TokenLocation
				{
					File = fileName,
					Line = 1,
					Index = 0,
					LineStartIndex = 0
				}
			};
		}

		public static Lexer FromString(string str, IErrorHandler errorHandler, string fileName = "string")
		{
			return new Lexer
			{
				_errorHandler = errorHandler,
				_text = str.Replace("\r\n", "\n", StringComparison.InvariantCulture),
				_location = new TokenLocation
				{
					File = fileName,
					Line = 1,
				}
			};
		}

		public Token PeekToken()
		{
			if (peek == null)
			{
				peek = NextToken();
				//UndoToken();
			}

			return peek;
		}

		public Token NextToken()
		{
			if (peek != null)
			{
				Token t = peek;
				peek = null;
				return t;
			}

			if (SkipWhitespaceAndComments(out TokenLocation loc))
			{
				loc.End = loc.Index;
				Token tok = new Token();
				tok.Location = loc;
				tok.Type = TokenType.NewLine;
				return tok;
			}

			return ReadToken();
		}



		private StringBuilder tokenDataBuilder = new StringBuilder();
		private Token ReadToken()
		{
			var token = new Token();
			token.Location = _location.Clone();
			token.Location.End = token.Location.Index;
			token.Type = TokenType.EOF;
			if (_location.Index >= _text.Length)
				return token;

			tokenDataBuilder.Clear();

			switch (Current)
			{
				case '=' when Next == '>': SimpleToken(ref token, TokenType.Arrow, 2); break;
				case '=' when Next == '=': SimpleToken(ref token, TokenType.DoubleEqual, 2); break;
				case '!' when Next == '=': SimpleToken(ref token, TokenType.NotEqual, 2); break;
				case '<' when Next == '=': SimpleToken(ref token, TokenType.LessEqual, 2); break;
				case '<' when Next == '<': SimpleToken(ref token, TokenType.LessLess, 2); break;
				case '>' when Next == '>': SimpleToken(ref token, TokenType.GreaterGreater, 2); break;
				case '>' when Next == '=': SimpleToken(ref token, TokenType.GreaterEqual, 2); break;
				case '+' when Next == '=': SimpleToken(ref token, TokenType.AddEq, 2); break;
				case '-' when Next == '=': SimpleToken(ref token, TokenType.SubEq, 2); break;
				case '*' when Next == '=': SimpleToken(ref token, TokenType.MulEq, 2); break;
				case '/' when Next == '=': SimpleToken(ref token, TokenType.DivEq, 2); break;
				case '%' when Next == '=': SimpleToken(ref token, TokenType.ModEq, 2); break;
				case '.' when Next == '.': SimpleToken(ref token, TokenType.PeriodPeriod, 2); break;
				case '&' when Next == '&': SimpleToken(ref token, TokenType.LogicalAnd, 2); break;
				case '[' when Next == ']': SimpleToken(ref token, TokenType.ArrayDef, 2); break;
				case '|' when Next == '|': SimpleToken(ref token, TokenType.LogicalOr, 2); break;
				case '/' when (Next == '/' && GetChar(2) == '/'):
					{
						// doc comment

						if (GetChar(3) == ' ')
						{
							token.Type = TokenType.DocComment;
							int index = 0;
							while (_location.Index < _text.Length && Current != '\n')
							{
								if (index >= 4)
									tokenDataBuilder.Append(Current);
								_location.Index += 1;
								index += 1;
							}
							if (_location.Index < _text.Length && Current == '\n')
							{
								_location.Index += 1;
								_location.Line++;
								_location.LineStartIndex = _location.Index;
							}
							token.Data = tokenDataBuilder.ToString();
						}
						else if (GetChar(3) == '*')
						{
							token.Type = TokenType.DocComment;
							token.Data = ParseMultiLineDocComment();
						}
						else
						{
							throw new Exception("this shouldn't happen");
						}

						break;
					}
				case '~': SimpleToken(ref token, TokenType.Tilda); break;
				case ':': SimpleToken(ref token, TokenType.Colon); break;
				case ';': SimpleToken(ref token, TokenType.Semicolon); break;
				case '.': SimpleToken(ref token, TokenType.Period); break;
				case '=': SimpleToken(ref token, TokenType.Equal); break;
				case '(': SimpleToken(ref token, TokenType.OpenParen); break;
				case ')': SimpleToken(ref token, TokenType.CloseParen); break;
				case '{': SimpleToken(ref token, TokenType.OpenBrace); break;
				case '}': SimpleToken(ref token, TokenType.CloseBrace); break;
				case '[': SimpleToken(ref token, TokenType.OpenBracket); break;
				case ']': SimpleToken(ref token, TokenType.CloseBracket); break;
				case ',': SimpleToken(ref token, TokenType.Comma); break;
				case '&': SimpleToken(ref token, TokenType.Ampersand); break;
				case '^': SimpleToken(ref token, TokenType.Hat); break;
				case '*': SimpleToken(ref token, TokenType.Asterisk); break;
				case '/': SimpleToken(ref token, TokenType.ForwardSlash); break;
				case '+': SimpleToken(ref token, TokenType.Plus); break;
				case '%': SimpleToken(ref token, TokenType.Percent); break;
				case '-': SimpleToken(ref token, TokenType.Minus); break;
				case '<': SimpleToken(ref token, TokenType.Less); break;
				case '>': SimpleToken(ref token, TokenType.Greater); break;
				case '!': SimpleToken(ref token, TokenType.Bang); break;
				case '|': SimpleToken(ref token, TokenType.VerticalSlash); break;

				case '"': ParseStringLiteral(ref token, '"'); break;
				case '\'':
					{
						ParseStringLiteral(ref token, '\'');
						token.Type = TokenType.CharLiteral;
						break;
					}

				case char cc when IsDigit(cc):
					ParseNumberLiteral(ref token);
					break;

				case '$': ParseIdentifier(ref token, TokenType.DollarIdentifier); break;
				case '#': ParseIdentifier(ref token, TokenType.SharpIdentifier); break;
				case '@': ParseIdentifier(ref token, TokenType.AtSignIdentifier); break;

				case char cc when IsIdentBegin(cc):
					ParseIdentifier(ref token, TokenType.Identifier);
					CheckKeywords(ref token);
					break;

				default:
					token.Type = TokenType.Unknown;
					_location.Index += 1;
					break;
			}

			if (token.Type == TokenType.StringLiteral || token.Type == TokenType.NumberLiteral || token.Type == TokenType.CharLiteral)
			{
				if (IsIdentBegin(Current))
				{
					token.Suffix = "" + Current;
					_location.Index++;

					while (IsIdent(Current))
					{
						token.Suffix += Current;
						_location.Index++;
					}
				}
			}

			token.Location.End = _location.Index;
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

				case "true": token.Type = TokenType.KwTrue; break;
				case "false": token.Type = TokenType.KwFalse; break;
				case "null": token.Type = TokenType.KwNull; break;

				case "using": token.Type = TokenType.KwUsing; break;

				case "if": token.Type = TokenType.KwIf; break;
				case "else": token.Type = TokenType.KwElse; break;
				case "switch": token.Type = TokenType.KwSwitch; break;
				case "case": token.Type = TokenType.KwCase; break;
				case "for": token.Type = TokenType.KwFor; break;
				case "while": token.Type = TokenType.KwWhile; break;

				case "break": token.Type = TokenType.KwBreak; break;
				case "continue": token.Type = TokenType.KwContinue; break;
				case "return": token.Type = TokenType.KwReturn; break;

				case "const": token.Type = TokenType.KwConst; break;
				case "default": token.Type = TokenType.KwDefault; break;
				case "new": token.Type = TokenType.KwNew; break;

				case "in": token.Type = TokenType.KwIn; break;
				case "is": token.Type = TokenType.KwIs; break;
				case "as": token.Type = TokenType.KwAs; break;

				case "public": token.Type = TokenType.KwPublic; break;
				case "protected": token.Type = TokenType.KwProtected; break;
				case "private": token.Type = TokenType.KwPrivate; break;

				case "async": token.Type = TokenType.KwAsync; break;

				case "static": token.Type = TokenType.KwStatic; break;

				case "abstract": token.Type = TokenType.KwAbstract; break;
				case "virtual": token.Type = TokenType.KwVirtual; break;
				case "override": token.Type = TokenType.KwOverride; break;
				case "partial": token.Type = TokenType.KwPartial; break;
				case "extern": token.Type = TokenType.KwExtern; break;
			}
		}

		private void SimpleToken(ref Token token, TokenType type, int len = 1)
		{
			token.Type = type;
			_location.Index += len;
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
