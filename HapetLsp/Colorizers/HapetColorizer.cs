using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using HapetLsp.Entities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace HapetLsp.Colorizers
{
    internal class HapetColorizer
    {
        public List<SemanticToken> CurrentSemanticTokens { get; } = new List<SemanticToken>();

        private readonly SemanticTokenType[] _tokenTypes;
        private readonly SemanticTokenModifier[] _tokenModifiers;
        private readonly ProgramFile _programFile;

        public HapetColorizer(ProgramFile file, SemanticTokenType[] tokenTypes, SemanticTokenModifier[] tokenModifiers) 
        {
            _tokenTypes = tokenTypes;
            _tokenModifiers = tokenModifiers;
            _programFile = file;
        }

        public void Colorize()
        {
            ColorizeUsings(_programFile);

            // making colorize of declarations
            foreach (var d in _programFile.Statements)
            {
                if (d is not AstDeclaration decl)
                    continue;
                ColorizeDeclaration(decl);
            }
        }

        private void ColorizeUsings(ProgramFile file)
        {
            foreach (var u in file.Usings)
            {
                CurrentSemanticTokens.Add(new SemanticToken(u.Location.Beginning.Line - 1, u.Location.Beginning.Column - 1,
                    u.Location.Ending.End - u.Location.Beginning.Index, _tokenTypes[1], _tokenModifiers[0]));
            }
        }

        private void ColorizeDeclaration(AstDeclaration decl)
        {
            // colorize special keys
            foreach (var k in decl.SpecialKeys)
            {
                CurrentSemanticTokens.Add(new SemanticToken(k.Location.Beginning.Line - 1, k.Location.Beginning.Column - 1,
                    k.Location.Ending.End - k.Location.Beginning.Index, _tokenTypes[2], _tokenModifiers[0]));
            }

            switch (decl)
            {
                case AstClassDecl clsD:
                    ColorizeClassDecl(clsD);
                    break;
            }
        }

        private void ColorizeClassDecl(AstClassDecl decl)
        {

        }
    }
}
