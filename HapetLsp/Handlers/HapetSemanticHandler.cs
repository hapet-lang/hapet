using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.ProjectParser;
using HapetLsp.Colorizers;
using HapetLsp.Entities;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Xml.Linq;

namespace HapetLsp.Handlers
{
    public class HapetSemanticHandler : SemanticTokensHandlerBase
    {
        private readonly static SemanticTokenType[] _tokenTypes = new[] 
        { 
            new SemanticTokenType("class"),         // 0
            new SemanticTokenType("using"),         // 1
            new SemanticTokenType("special_key"),   // 2
            new SemanticTokenType("interface"),     // 3
            new SemanticTokenType("struct"),        // 4
            new SemanticTokenType("func"),          // 5
            new SemanticTokenType("var"),           // 6
            new SemanticTokenType("enum"),          // 7
            new SemanticTokenType("purple"),        // 8
            new SemanticTokenType("number"),        // 9
            new SemanticTokenType("string"),        // 10
            new SemanticTokenType("char"),          // 11
            new SemanticTokenType("comment"),       // 12
        };
        private readonly static SemanticTokenModifier[] _tokenModifiers = new[] 
        { 
            new SemanticTokenModifier("static"),
            new SemanticTokenModifier("declaration"),
        };

        private readonly Compiler _compiler;
        private readonly Dictionary<ProgramFile, HapetColorizer> _fileColorizers = new Dictionary<ProgramFile, HapetColorizer>();

        public HapetSemanticHandler(Compiler compiler)
        {
            _compiler = compiler;
        }

        protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
        {
            return new SemanticTokensRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(
                     new TextDocumentFilter { Pattern = "**/*.hpt" }
                ),
                Legend = new SemanticTokensLegend()
                {
                    TokenTypes = _tokenTypes,
                    TokenModifiers = _tokenModifiers
                },
                Full = new SemanticTokensCapabilityRequestFull { Delta = false },
                Range = true
            };
        }

        protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));
        }

        async protected override Task Tokenize(
            SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            var path = DocumentUri.GetFileSystemPath(identifier);
            var file = _compiler.GetFile(path);
            if (file == null)
                return;

            // add colorizer if not exists
            HapetColorizer colorizer;
            if (!_fileColorizers.TryGetValue(file, out colorizer))
            {
                colorizer = new HapetColorizer(file, _compiler, _tokenTypes, _tokenModifiers);
                _fileColorizers[file] = colorizer;
            }
            // colorize
            colorizer.Colorize();

            // sort and add to builder
            colorizer.CurrentSemanticTokens.Sort((a, b) =>
            {
                var lineCompare = a.Line.CompareTo(b.Line);
                if (lineCompare != 0) return lineCompare;
                return a.Offset.CompareTo(b.Offset);
            });
            foreach (var t in colorizer.CurrentSemanticTokens)
            {
                builder.Push(t.Line, t.Offset, t.Width, t.TokenType, t.TokenModifier);
            }
            colorizer.CurrentSemanticTokens.Clear();
        }
    }
}
