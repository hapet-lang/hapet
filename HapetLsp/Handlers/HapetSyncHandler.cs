using HapetFrontend;
using HapetFrontend.ProjectParser;
using HapetLastPrepare;
using HapetPostPrepare;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace HapetLsp.Handlers
{
    public partial class HapetSyncHandler : ITextDocumentSyncHandler
    {
        private readonly ILanguageServerFacade _facade;
        private readonly Compiler _compiler;
        private readonly PostPrepare _postPrepare;
        private readonly LastPrepare _lastPrepare;

        private readonly TextDocumentSelector _documentSelector = new TextDocumentSelector(
            new TextDocumentFilter()
            {
                Pattern = "**/*.hpt"
            }
        );

        public HapetSyncHandler(ILanguageServerFacade facade, Compiler compiler, PostPrepare pp, LastPrepare lp)
        {
            _facade = facade;
            _compiler = compiler;
            _postPrepare = pp;
            _lastPrepare = lp;
        }

        public TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
        {
            return new TextDocumentAttributes(uri, "hapet");
        }

        public Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        {
            var path = DocumentUri.GetFileSystemPath(request.TextDocument.Uri);
            var file = _compiler.GetFile(path);
            if (file == null)
                return Unit.Task;
            var colorizer = HapetSemanticHandler.CreateColorizer(file, _compiler);

            var contentChange = request.ContentChanges.FirstOrDefault();
            if (contentChange == null)
                return Unit.Task;

            // get delta text
            var text = contentChange.Text.Replace("\r\n", "\n", StringComparison.InvariantCulture);
            if (text == string.Empty)
           {
                // delete 
            }
            else if (text != string.Empty && contentChange.RangeLength > 0)
            {
                // change
            }
            else
            {
                // add
                ReparseLocationOnAdd(colorizer, text, contentChange.Range);
            }
            return Unit.Task;
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
        {
            var path = DocumentUri.GetFileSystemPath(request.TextDocument.Uri);
            var file = _compiler.GetFile(path);
            if (file == null)
                return Unit.Task;

            HapetSemanticHandler.CreateColorizer(file, _compiler);
            return Unit.Task;
        }

        public Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }

        public Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }

        TextDocumentChangeRegistrationOptions IRegistration<TextDocumentChangeRegistrationOptions, TextSynchronizationCapability>.GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                SyncKind = TextDocumentSyncKind.Incremental,
            };
        }

        TextDocumentOpenRegistrationOptions IRegistration<TextDocumentOpenRegistrationOptions, TextSynchronizationCapability>.GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentOpenRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }

        TextDocumentCloseRegistrationOptions IRegistration<TextDocumentCloseRegistrationOptions, TextSynchronizationCapability>.GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentCloseRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions, TextSynchronizationCapability>.GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentSaveRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                IncludeText = false,
            };
        }
    }
}
