using HapetFrontend.Ast;

namespace HapetFrontend.Parsing
{
    public partial class Lexer
    {
        private bool SkipWhitespaceAndComments(TokenLocation location, out TokenLocation loc, bool skipWhitespaces = true)
        {
            loc = null;

            while (location.Index < _text.Length)
            {
                char c = Current(location);
                if (c == '/' && Next(location) == '*')
                {
                    ParseMultiLineComment(location);
                }

                else if (c == '/' && Next(location) == '/')
                {
                    if (GetChar(2, location) == '/')
                    {
                        // potentially doc comment

                        if (GetChar(3, location) == ' ')
                        {
                            // single line doc comment
                            break;
                        }
                        else if (GetChar(3, location) == '*')
                        {
                            ParseMultiLineDocComment(location);
                            break;
                        }
                    }
                    ParseSingleLineComment(location);
                }

                else if (c == ' ' || c == '\t')
                {
                    if (skipWhitespaces)
                        location.Index++;
                    else
                        break;
                }

                else if (c == '\r')
                {
                    location.Index++;
                }

                else if (c == '\n')
                {
                    if (loc == null)
                    {
                        loc = location.Clone();
                    }

                    location.Line++;
                    location.Index++;
                    location.LineStartIndex = location.Index;
                }

                else break;
            }

            if (loc != null)
            {
                loc.End = location.Index;
                return true;
            }

            return false;
        }

        private void ParseSingleLineComment(TokenLocation location)
        {
            while (location.Index < _text.Length)
            {
                if (Current(location) == '\n')
                    break;
                location.Index++;
            }
        }

        private void ParseMultiLineComment(TokenLocation location)
        {

            int level = 0;
            while (location.Index < _text.Length)
            {
                char curr = Current(location);
                char next = Next(location);
                location.Index++;

                if (curr == '/' && next == '*')
                {
                    location.Index++;
                    level++;
                }

                else if (curr == '*' && next == '/')
                {
                    location.Index++;
                    level--;

                    if (level == 0)
                        break;
                }

                else if (curr == '\n')
                {
                    location.Index++;
                    location.LineStartIndex = location.Index;
                }
            }
        }

        private string ParseMultiLineDocComment(TokenLocation location)
        {
            int startIndex = location.Index + 4;
            int initialIndentation = location.Column;

            int endIndex = startIndex;

            int level = 0;
            while (location.Index < _text.Length)
            {
                char curr = Current(location);
                char next = Next(location);
                location.Index++;

                if (curr == '/' && next == '*')
                {
                    location.Index++;
                    level++;
                }
                else if (curr == '/' && next == '/' && GetChar(1, location) == '*' && GetChar(2, location) == '/')
                {
                    location.Index += 3;
                    level--;

                    if (level == 0)
                    {
                        break;
                    }
                }
                else if (curr == '*' && next == '/')
                {
                    location.Index++;
                    level--;

                    if (level == 0)
                    {
                        break;
                    }
                }
                else if (curr == '\n')
                {
                    endIndex = location.Index - 1;
                    location.Index++;
                    location.LineStartIndex = location.Index;
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
