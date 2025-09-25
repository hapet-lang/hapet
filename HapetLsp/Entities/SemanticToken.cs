using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace HapetLsp.Entities
{
    internal struct SemanticToken
    {
        public int Line;
        public int Offset;
        public int Width;
        public SemanticTokenType TokenType;
        public SemanticTokenModifier TokenModifier;

        public SemanticToken(int line, int offset, int width, SemanticTokenType type, SemanticTokenModifier modifier)
        {
            Line = line;
            Offset = offset;
            Width = width;
            TokenType = type;
            TokenModifier = modifier;
        }
    }
}
