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
            // skip synthetic
            if (decl.IsSyntheticStatement)
                return;

            // colorize special keys
            ColorizeSpecialKeys(decl);

            switch (decl)
            {
                case AstClassDecl clsD:
                    ColorizeClassDecl(clsD);
                    break;
                case AstStructDecl strD:
                    ColorizeStructDecl(strD);
                    break;
                case AstFuncDecl funcD:
                    ColorizeFuncDecl(funcD);
                    break;
            }
        }

        private void ColorizeClassDecl(AstClassDecl decl)
        {
            // class token
            AddSemanticToken(decl.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);
            // class name
            AddSemanticToken(decl.Name.Location, _tokenTypes[0], _tokenModifiers[0]);

            foreach (var i in decl.InheritedFrom)
            {
                // skip synthetic
                if (i.IsSyntheticStatement)
                    continue;
                ColorizeDependingOnType(i);
            }

            foreach (var d in decl.Declarations)
            {
                ColorizeDeclaration(d);
            }
        }

        private void ColorizeStructDecl(AstStructDecl decl)
        {
            // struct token
            AddSemanticToken(decl.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);
            // struct name
            AddSemanticToken(decl.Name.Location, _tokenTypes[4], _tokenModifiers[0]);

            foreach (var i in decl.InheritedFrom)
            {
                // skip synthetic
                if (i.IsSyntheticStatement)
                    continue;
                ColorizeDependingOnType(i);
            }

            foreach (var d in decl.Declarations)
            {
                ColorizeDeclaration(d);
            }
        }

        private void ColorizeFuncDecl(AstFuncDecl decl)
        {
            if (!decl.Returns.IsSyntheticStatement)
                ColorizeDependingOnType(decl.Returns);
            // func name
            AddSemanticToken(decl.Name.Location, _tokenTypes[5], _tokenModifiers[0]);

            // params
            foreach (var p in decl.Parameters)
            {
                if (p.IsSyntheticStatement)
                    continue;
                ColorizeParamDecl(p);
            }
        }

        private void ColorizeParamDecl(AstParamDecl decl)
        {
            ColorizeDependingOnType(decl.Type);
            // param name
            AddSemanticToken(decl.Name.Location, _tokenTypes[6], _tokenModifiers[0]);
        }

        private void ColorizeDependingOnType(AstExpression expr)
        {
            if (expr.OutType is ClassType clsTT)
                AddSemanticToken(expr.Location, clsTT.Declaration.IsInterface ? _tokenTypes[3] : _tokenTypes[0], _tokenModifiers[0]);
            else
                AddSemanticToken(expr.Location, _tokenTypes[4], _tokenModifiers[0]);
        }

        private void AddSemanticToken(ILocation location, SemanticTokenType type, SemanticTokenModifier modifier)
        {
            CurrentSemanticTokens.Add(new SemanticToken(location.Beginning.Line - 1, location.Beginning.Column - 1,
                    location.Ending.End - location.Beginning.Index, type, modifier));
        }
    }
}
