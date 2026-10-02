using HapetFrontend.Entities;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace HapetFrontend.Lexer
{
    /// <summary>
    /// The class is taken from Akbura https://github.com/Asaicraft/Akbura
    /// </summary>
    internal struct SlidingTextWindow
    {
        public const char InvalidCharacter = char.MaxValue;

        public const int DefaultWindowLength = 1024;

        public SourceText Text { get; }

        private readonly int _textEnd;
        private int _positionInText;

        private ArraySegment<char> _characterWindow;
        private int _characterWindowStartPositionInText;
        private readonly QuickQuickSet<string> _strings;

        public SlidingTextWindow(SourceText text)
        {
            Text = text;
            _textEnd = text.Length;
            _characterWindow = new char[DefaultWindowLength];
            _strings = new QuickQuickSet<string>();

            ReadChunkAt(0);
        }

        private void ReadChunkAt(int position)
        {
            position = Math.Min(position, _textEnd);

            var amountToRead = Math.Min(_textEnd - position, DefaultWindowLength);

            Text.CopyTo(
                position,
                _characterWindow.Array!,
                0,
                amountToRead);

            _characterWindowStartPositionInText = position;
            _characterWindow = new(_characterWindow.Array!, 0, amountToRead);
        }

        public readonly int Position => _positionInText;

        public readonly ReadOnlySpan<char> CurrentWindowSpan
        {
            get
            {
                var start = _positionInText - _characterWindowStartPositionInText;

                return start < 0 || start >= _characterWindow.Count
                    ? default
                    : _characterWindow.AsSpan(start);
            }
        }

        private readonly int CharacterWindowEndPositionInText =>
            _characterWindowStartPositionInText + _characterWindow.Count;

        private readonly bool PositionIsWithinWindow(int position)
        {
            return position >= _characterWindowStartPositionInText &&
                   position < CharacterWindowEndPositionInText;
        }

        public void Reset(int position)
        {
            _positionInText = Math.Min(position, _textEnd);

            if (PositionIsWithinWindow(_positionInText))
            {
                return;
            }

            ReadChunkAt(_positionInText);
        }

        public readonly bool IsReallyAtEnd()
        {
            return Position >= _textEnd;
        }

        public void AdvanceChar(int count)
        {
            _positionInText += count;
        }

        public char PeekChar()
        {
            if (IsReallyAtEnd())
            {
                return InvalidCharacter;
            }

            var position = _positionInText;

            if (!PositionIsWithinWindow(position))
            {
                ReadChunkAt(position);
            }

            return _characterWindow.Array![position - _characterWindowStartPositionInText];
        }

        public readonly string Intern(StringBuilder text) => _strings.Intern(text);
        public readonly string Intern(char[] array, int start, int length) => Intern(array.AsSpan(start, length));
        public readonly string Intern(ReadOnlySpan<char> chars) => _strings.Intern(chars);
    }
}
