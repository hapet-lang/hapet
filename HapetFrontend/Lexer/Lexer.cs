using HapetFrontend.Syntax.Green;

namespace HapetFrontend.Lexer
{
    internal partial class Lexer
    {
        private SlidingTextWindow _textProvider;

        public Lexer(SlidingTextWindow textProvider)
        {
            _textProvider = textProvider;
        }
    }
}
