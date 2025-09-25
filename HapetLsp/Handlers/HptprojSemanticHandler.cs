using HapetLsp.Entities;
using Microsoft.Language.Xml;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Xml;

namespace HapetLsp.Handlers
{
    public class HptprojSemanticHandler : SemanticTokensHandlerBase
    {
        private readonly static SemanticTokenType[] _tokenTypes = new[] { new SemanticTokenType("tag"), new SemanticTokenType("param"), new SemanticTokenType("comment") };
        private readonly static SemanticTokenModifier[] _tokenModifiers = new[] { new SemanticTokenModifier("default") };

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
            var path = DocumentUri.GetFileSystemPath(identifier);
            var content = (await File.ReadAllTextAsync(path, cancellationToken)).Replace("\r\n", "\n");
            var syntaxTree = Parser.ParseText(content);

            _currentSplittedFileText = content.Split('\n');

            ColorizeNode(syntaxTree.Root, builder);

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

        private string[] _currentSplittedFileText = null;
        private (int line, int offset) GetLineNumberAndOffsetByIndex(int index)
        {
            if (_currentSplittedFileText == null)
                return (0, 0);
            int currentIndexSum = 0;
            int currentLineNumber = 0;
            foreach (var line in _currentSplittedFileText)
            {
                var prevIndexSum = currentIndexSum;
                currentIndexSum += line.Length + 1; // + 1 is for \n
                if (currentIndexSum > index)
                    return (currentLineNumber, index - prevIndexSum);
                currentLineNumber++;
            }
            Debug.Assert(false, "Should not be here");
            return (currentLineNumber, -1); // should not be here
        }

        private (List<int> lines, List<int> offsets, List<int> widths) GetLinesAndOffsetsForComment(int start, int end)
        {
            if (_currentSplittedFileText == null)
                return ([0], [0], [0]);

            List<int> lines = new List<int>();
            List<int> offsets = new List<int>();
            List<int> widths = new List<int>();

            int currentIndexSum = 0;
            int currentLineNumber = 0;
            for (int i = 0; i < _currentSplittedFileText.Length; ++i)
            {
                var line = _currentSplittedFileText[i];
                var prevIndexSum = currentIndexSum;
                currentIndexSum += line.Length + 1; // + 1 is for \n
                if (currentIndexSum > start)
                {
                    lines.Add(currentLineNumber);
                    // if there are already elements - offset is 0
                    int offset = offsets.Count == 0 ? start - prevIndexSum : 0;
                    offsets.Add(offset);
                    // check that it is the last comment line
                    bool lastLine = (i + 1 < _currentSplittedFileText.Length) ||
                        currentIndexSum + _currentSplittedFileText[i + 1].Length > end;
                    int width = lastLine ? (end - prevIndexSum) : (line.Length - offset);
                    widths.Add(width);
                }

                if (currentIndexSum > end)
                    break;
                currentLineNumber++;
            }
            return (lines, offsets, widths);
        }

        private void ColorizeNode(object element, SemanticTokensBuilder builder)
        {
            // checks
            if (element is not XmlElementSyntax xmlElement)
            {
                // colorize comment
                if (element is XmlCommentSyntax xmlComment)
                {
                    var (lines, offsets, widths) = GetLinesAndOffsetsForComment(xmlComment.Span.Start, xmlComment.Span.End);
                    for (int i = 0; i < lines.Count; i++)
                    {
                        _currentSemanticTokens.Add(new SemanticToken(lines[i], offsets[i], widths[i], _tokenTypes[2], _tokenModifiers[0]));
                    }
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
                var (line, offset) = GetLineNumberAndOffsetByIndex(xmlElement.StartTag.SpanStart);
                _currentSemanticTokens.Add(new SemanticToken(line, offset, xmlElement.StartTag.Width, _tokenTypes[0], _tokenModifiers[0]));
            }
            // coloring end tag
            if (xmlElement.EndTag != null)
            {
                var (line, offset) = GetLineNumberAndOffsetByIndex(xmlElement.EndTag.SpanStart);
                _currentSemanticTokens.Add(new SemanticToken(line, offset, xmlElement.EndTag.Width, _tokenTypes[0], _tokenModifiers[0]));
            }
        }
    }
}
