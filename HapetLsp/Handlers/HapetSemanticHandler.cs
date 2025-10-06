using HapetFrontend;
using HapetFrontend.ProjectParser;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace HapetLsp.Handlers
{
    public class HapetSemanticHandler : SemanticTokensHandlerBase
    {
        private readonly static SemanticTokenType[] _tokenTypes = new[] 
        { 
            new SemanticTokenType("class"),
        };
        private readonly static SemanticTokenModifier[] _tokenModifiers = new[] 
        { 
            new SemanticTokenModifier("static"),
            new SemanticTokenModifier("declaration"),
        };

        private readonly Compiler _compiler;

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
        }
    }
}
