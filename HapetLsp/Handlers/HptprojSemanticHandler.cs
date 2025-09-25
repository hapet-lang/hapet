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
        private readonly static SemanticTokenType[] _tokenTypes = new[] { new SemanticTokenType("tag"), new SemanticTokenType("param") };
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
                currentIndexSum += line.Length + 1;
                if (currentIndexSum > index)
                    // - currentLineNumber is needed to handle \n chars that were removed
                    return (currentLineNumber, index - prevIndexSum);
                currentLineNumber++;
            }
            Debug.Assert(false, "Should not be here");
            return (currentLineNumber, -1); // should not be here
        }

        private void ColorizeNode(IXmlElement element, SemanticTokensBuilder builder)
        {
            if (element is not XmlElementSyntax xmlElement)
            {
                // TODO: comments and other
                return;
            }

            foreach (var c in xmlElement.Content)
            {
                if (c is XmlElementSyntax cElement)
                    ColorizeNode(cElement, builder);
            }

            if (xmlElement.StartTag != null)
            {
                var (line, offset) = GetLineNumberAndOffsetByIndex(xmlElement.StartTag.SpanStart);
                _currentSemanticTokens.Add(new SemanticToken(line, offset, xmlElement.StartTag.Width, _tokenTypes[0], _tokenModifiers[0]));
            }
            if (xmlElement.EndTag != null)
            {
                var (line, offset) = GetLineNumberAndOffsetByIndex(xmlElement.EndTag.SpanStart);
                Console.WriteLine($"{line} : {offset}");
                _currentSemanticTokens.Add(new SemanticToken(line, offset, xmlElement.EndTag.Width, _tokenTypes[0], _tokenModifiers[0]));
            }
        }
    }
}
