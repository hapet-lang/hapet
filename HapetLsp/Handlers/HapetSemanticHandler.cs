using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.ProjectParser;
using HapetLastPrepare;
using HapetLsp.Colorizers;
using HapetLsp.Entities;
using HapetPostPrepare;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Collections.ObjectModel;
using System.Text;
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
            new SemanticTokenType("not_compiled"),  // 13
            new SemanticTokenType("directive"),     // 14
        };
        private readonly static SemanticTokenModifier[] _tokenModifiers = new[] 
        { 
            new SemanticTokenModifier("static"),
            new SemanticTokenModifier("declaration"),
        };

        private readonly ILanguageServerFacade _facade;
        private readonly Compiler _compiler;
        private readonly ProjectXmlParser _projectXmlParser;
        private readonly PostPrepare _postPrepare;
        private readonly LastPrepare _lastPrepare;
        private readonly Action _projectResolver;
        internal static Dictionary<ProgramFile, HapetColorizer> FileColorizers { get; } = new Dictionary<ProgramFile, HapetColorizer>();

        public HapetSemanticHandler(ILanguageServerFacade facade, Compiler compiler, ProjectXmlParser projectParser, PostPrepare pp, LastPrepare lp, Action projectResolver)
        {
            _facade = facade;
            _compiler = compiler;
            _postPrepare = pp;
            _lastPrepare = lp;
            _projectResolver = projectResolver;
            _projectXmlParser = projectParser;
        }

        protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
        {
            return HapetSyncHandler.CreateDefaultRegistrationOptions("**/*.hpt", _tokenTypes, _tokenModifiers);
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
            file ??= _compiler.AddFile(path);

            HapetSyncHandler.ReparseWholeProject(_compiler, _projectXmlParser, _postPrepare, _lastPrepare, _projectResolver);

            var colorizer = CreateColorizer(file, _compiler);
            // clear all color tokens
            colorizer.CurrentSemanticTokens.Clear();
            // colorize
            colorizer.Colorize();
            // sort and add to builder
            colorizer.SortTokens();
            foreach (var (t, _) in colorizer.CurrentSemanticTokens)
            {
                builder.Push(t.Line, t.Offset, t.Width, t.TokenType, t.TokenModifier);
            }

            HapetSyncHandler.SendMessages(_compiler, _projectXmlParser, _facade);
        }

        internal static HapetColorizer CreateColorizer(ProgramFile file, Compiler compiler)
        {
            if (FileColorizers.TryGetValue(file, out var colorizer))
                return colorizer;

            // add colorizer if not exists
            colorizer = new HapetColorizer(file, compiler, _tokenTypes, _tokenModifiers);
            FileColorizers[file] = colorizer;
            // colorize
            colorizer.Colorize();
            // sort and add to builder
            colorizer.SortTokens();
            return colorizer;
        }
    }
}
