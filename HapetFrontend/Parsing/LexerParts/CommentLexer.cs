using HapetFrontend.Ast;

namespace HapetFrontend.Parsing
{
	public partial class Lexer
	{
		private bool SkipWhitespaceAndComments(out TokenLocation loc)
		{
			loc = null;

			while (_location.Index < _text.Length)
			{
				char c = Current;
				if (c == '/' && Next == '*')
				{
					ParseMultiLineComment();
				}

				else if (c == '/' && Next == '/')
				{
					if (GetChar(2) == '/')
					{
						// potentially doc comment

						if (GetChar(3) == ' ')
						{
							// single line doc comment
							break;
						}
						else if (GetChar(3) == '*')
						{
							ParseMultiLineDocComment();
							break;
						}
					}
					ParseSingleLineComment();
				}

				else if (c == ' ' || c == '\t')
				{
					_location.Index++;
				}

				else if (c == '\r')
				{
					_location.Index++;
				}

				else if (c == '\n')
				{
					if (loc == null)
					{
						loc = _location.Clone();
					}

					_location.Line++;
					_location.Index++;
					_location.LineStartIndex = _location.Index;
				}

				else break;
			}

			if (loc != null)
			{
				loc.End = _location.Index;
				return true;
			}

			return false;
		}

		private void ParseSingleLineComment()
		{
			while (_location.Index < _text.Length)
			{
				if (Current == '\n')
					break;
				_location.Index++;
			}
		}

		private void ParseMultiLineComment()
		{

			int level = 0;
			while (_location.Index < _text.Length)
			{
				char curr = Current;
				char next = Next;
				_location.Index++;

				if (curr == '/' && next == '*')
				{
					_location.Index++;
					level++;
				}

				else if (curr == '*' && next == '/')
				{
					_location.Index++;
					level--;

					if (level == 0)
						break;
				}

				else if (curr == '\n')
				{
					_location.Index++;
					_location.LineStartIndex = _location.Index;
				}
			}
		}

		private string ParseMultiLineDocComment()
		{
			int startIndex = _location.Index + 4;
			int initialIndentation = _location.Column;

			int endIndex = startIndex;

			int level = 0;
			while (_location.Index < _text.Length)
			{
				char curr = Current;
				char next = Next;
				_location.Index++;

				if (curr == '/' && next == '*')
				{
					_location.Index++;
					level++;
				}
				else if (curr == '/' && next == '/' && GetChar(1) == '*' && GetChar(2) == '/')
				{
					_location.Index += 3;
					level--;

					if (level == 0)
					{
						break;
					}
				}
				else if (curr == '*' && next == '/')
				{
					_location.Index++;
					level--;

					if (level == 0)
					{
						break;
					}
				}
				else if (curr == '\n')
				{
					endIndex = _location.Index - 1;
					_location.Index++;
					_location.LineStartIndex = _location.Index;
				}
			}

			if (startIndex >= _text.Length)
				startIndex = _text.Length - 1;

			if (endIndex >= _text.Length)
				endIndex = _text.Length - 1;

			return string.Join("\n", _text.Substring(startIndex, endIndex - startIndex)
				.Split("\n")
				.Select(part =>
				{
					int i = 0;
					for (; i < initialIndentation - 1 && i < part.Length && part[i] == ' '; i++) ;
					return part.Substring(i);
				}));
		}
	}
}
