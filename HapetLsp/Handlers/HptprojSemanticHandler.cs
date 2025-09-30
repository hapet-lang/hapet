using HapetFrontend.ProjectParser;
using HapetLsp.Entities;
using Microsoft.Language.Xml;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace HapetLsp.Handlers
{
    public class HptprojSemanticHandler : SemanticTokensHandlerBase
    {
        private readonly static SemanticTokenType[] _tokenTypes = new[] { new SemanticTokenType("tag"), new SemanticTokenType("param"), new SemanticTokenType("comment"), new SemanticTokenType("bracket") };
        private readonly static SemanticTokenModifier[] _tokenModifiers = new[] { new SemanticTokenModifier("default") };

        private readonly ProjectXmlParser _projectXmlParser;

        public HptprojSemanticHandler(ProjectXmlParser projectParser)
        {
            _projectXmlParser = projectParser;
        }

        protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
        {
            return new SemanticTokensRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(
                     new TextDocumentFilter { Pattern = "**/*.hptproj" }
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
            if (_projectXmlParser.XmlProgramFile.FilePath != (new Uri(path, UriKind.Absolute)))
            {
                // expected the same project file
                return;
            }

            ColorizeNode(_projectXmlParser.XmlParsed.Root, builder);

            _currentSemanticTokens.Sort((a, b) =>
            {
                var lineCompare = a.Line.CompareTo(b.Line);
                if (lineCompare != 0) return lineCompare;
                return a.Offset.CompareTo(b.Offset);
            });
            foreach (var t in _currentSemanticTokens)
            {
                builder.Push(t.Line, t.Offset, t.Width, t.TokenType, t.TokenModifier);
            }
            _currentSemanticTokens.Clear();
        }
        private readonly List<SemanticToken> _currentSemanticTokens = new List<SemanticToken>();

        private void ColorizeNode(object element, SemanticTokensBuilder builder)
        {
            // checks
            if (element is not XmlElementSyntax xmlElement)
            {
                // colorize comment
                if (element is XmlCommentSyntax xmlComment)
                {
                    var (lines, offsets, widths) = _projectXmlParser.XmlProgramFile.GetLinesAndOffsetsForXmlComment(xmlComment.Span.Start, xmlComment.Span.End);
                    for (int i = 0; i < lines.Count; i++)
                    {
                        _currentSemanticTokens.Add(new SemanticToken(lines[i], offsets[i], widths[i], _tokenTypes[2], _tokenModifiers[0]));
                    }
                }
                // if <Asd/> tag
                else if (element is XmlEmptyElementSyntax xmlEmpty)
                {
                    XmlNameSyntax name = xmlEmpty.NameNode;
                    var (line, offset) = _projectXmlParser.XmlProgramFile.GetLineNumberAndOffsetByIndex(name.Span.Start);
                    _currentSemanticTokens.Add(new SemanticToken(line, offset, name.Width, _tokenTypes[0], _tokenModifiers[0]));
                    // brackets
                    (line, offset) = _projectXmlParser.XmlProgramFile.GetLineNumberAndOffsetByIndex(xmlEmpty.Span.Start);
                    _currentSemanticTokens.Add(new SemanticToken(line, offset, 1, _tokenTypes[3], _tokenModifiers[0]));
                    (line, offset) = _projectXmlParser.XmlProgramFile.GetLineNumberAndOffsetByIndex(xmlEmpty.Span.End);
                    _currentSemanticTokens.Add(new SemanticToken(line, offset - 2, 2, _tokenTypes[3], _tokenModifiers[0]));
                }
                // TODO: comments and other
                return;
            }

            // go over nested tags
            foreach (var c in xmlElement.Content)
            {
                ColorizeNode(c, builder);
            }

            // coloring start tag
            if (xmlElement.StartTag != null)
            {
                var (line, offset) = _projectXmlParser.XmlProgramFile.GetLineNumberAndOffsetByIndex(xmlElement.StartTag.SpanStart);
                _currentSemanticTokens.Add(new SemanticToken(line, offset + 1, xmlElement.StartTag.Width - 2, _tokenTypes[0], _tokenModifiers[0]));
                // brackets
                _currentSemanticTokens.Add(new SemanticToken(line, offset, 1, _tokenTypes[3], _tokenModifiers[0]));
                _currentSemanticTokens.Add(new SemanticToken(line, offset + xmlElement.StartTag.Width - 1, 1, _tokenTypes[3], _tokenModifiers[0]));
            }
            // coloring end tag
            if (xmlElement.EndTag != null)
            {
                var (line, offset) = _projectXmlParser.XmlProgramFile.GetLineNumberAndOffsetByIndex(xmlElement.EndTag.SpanStart);
                _currentSemanticTokens.Add(new SemanticToken(line, offset + 2, xmlElement.EndTag.Width - 3, _tokenTypes[0], _tokenModifiers[0]));
                // brackets
                _currentSemanticTokens.Add(new SemanticToken(line, offset, 2, _tokenTypes[3], _tokenModifiers[0]));
                _currentSemanticTokens.Add(new SemanticToken(line, offset + xmlElement.EndTag.Width - 1, 1, _tokenTypes[3], _tokenModifiers[0]));
            }
        }
    }
}
