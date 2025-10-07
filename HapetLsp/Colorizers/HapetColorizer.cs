using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using HapetFrontend.Types;
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
                AddSemanticToken(u.Location, _tokenTypes[1], _tokenModifiers[0]);
            }
        }

        private void ColorizeSpecialKeys(AstDeclaration decl)
        {
            // colorize special keys
            foreach (var k in decl.SpecialKeys)
            {
                AddSemanticToken(k.Location, _tokenTypes[2], _tokenModifiers[0]);
            }
        }

        private void ColorizeDeclaration(AstDeclaration decl)
        {
            // colorize special keys
            ColorizeSpecialKeys(decl);

            switch (decl)
            {
                case AstClassDecl clsD:
                    ColorizeClassDecl(clsD);
                    break;
                case AstFuncDecl funcD:
                    ColorizeFuncDecl(funcD);
                    break;
            }
        }

        private void ColorizeClassDecl(AstClassDecl decl)
        {
            foreach (var i in decl.InheritedFrom)
            {
                // skip synthetic
                if (i.IsSyntheticStatement)
                    continue;
                if (i.OutType is ClassType)
                    AddSemanticToken(i.Location, _tokenTypes[0], _tokenModifiers[0]);
                else
                    AddSemanticToken(i.Location, _tokenTypes[3], _tokenModifiers[0]);
            }

            foreach (var d in decl.Declarations)
            {
                ColorizeDeclaration(d);
            }
        }

        private void ColorizeFuncDecl(AstFuncDecl decl)
        {

        }

        private void AddSemanticToken(ILocation location, SemanticTokenType type, SemanticTokenModifier modifier)
        {
            CurrentSemanticTokens.Add(new SemanticToken(location.Beginning.Line - 1, location.Beginning.Column - 1,
                    location.Ending.End - location.Beginning.Index, type, modifier));
        }
    }
}
