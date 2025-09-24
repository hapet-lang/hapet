using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace HapetLsp.Handlers
{
    public class HptprojSemanticHandler : SemanticTokensHandlerBase
    {
        protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
        {
            return new SemanticTokensRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(
                     new TextDocumentFilter { Pattern = "**/*.hptproj" }
                ),
                Legend = new SemanticTokensLegend()
                {
                    TokenTypes = new[] { new SemanticTokenType("class") },
                    TokenModifiers = new[] { new SemanticTokenModifier("static") }
                },
                Full = new SemanticTokensCapabilityRequestFull { Delta = false },
                Range = true
            };
        }

        protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));
        }

        protected override Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
