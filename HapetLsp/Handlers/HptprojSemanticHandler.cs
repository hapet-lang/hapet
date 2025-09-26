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
        private readonly static SemanticTokenType[] _tokenTypes = new[] { new SemanticTokenType("tag"), new SemanticTokenType("param"), new SemanticTokenType("comment"), new SemanticTokenType("bracket") };
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

            //ColorizeNode(syntaxTree.Root, builder);

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
        

        //private void ColorizeNode(object element, SemanticTokensBuilder builder)
        //{
        //    // checks
        //    if (element is not XmlElementSyntax xmlElement)
        //    {
        //        // colorize comment
        //        if (element is XmlCommentSyntax xmlComment)
        //        {
        //            var (lines, offsets, widths) = GetLinesAndOffsetsForComment(xmlComment.Span.Start, xmlComment.Span.End);
        //            for (int i = 0; i < lines.Count; i++)
        //            {
        //                _currentSemanticTokens.Add(new SemanticToken(lines[i], offsets[i], widths[i], _tokenTypes[2], _tokenModifiers[0]));
        //            }
        //        }
        //        // if <Asd/> tag
        //        else if (element is XmlEmptyElementSyntax xmlEmpty)
        //        {
        //            XmlNameSyntax name = xmlEmpty.NameNode;
        //            var (line, offset) = GetLineNumberAndOffsetByIndex(name.Span.Start);
        //            _currentSemanticTokens.Add(new SemanticToken(line, offset, name.Width, _tokenTypes[0], _tokenModifiers[0]));
        //            // brackets
        //            (line, offset) = GetLineNumberAndOffsetByIndex(xmlEmpty.Span.Start);
        //            _currentSemanticTokens.Add(new SemanticToken(line, offset, 1, _tokenTypes[3], _tokenModifiers[0]));
        //            (line, offset) = GetLineNumberAndOffsetByIndex(xmlEmpty.Span.End);
        //            _currentSemanticTokens.Add(new SemanticToken(line, offset - 2, 2, _tokenTypes[3], _tokenModifiers[0]));
        //        }
        //        // TODO: comments and other
        //        return;
        //    }

        //    // go over nested tags
        //    foreach (var c in xmlElement.Content)
        //    {
        //        ColorizeNode(c, builder);
        //    }

        //    // coloring start tag
        //    if (xmlElement.StartTag != null)
        //    {
        //        var (line, offset) = GetLineNumberAndOffsetByIndex(xmlElement.StartTag.SpanStart);
        //        _currentSemanticTokens.Add(new SemanticToken(line, offset + 1, xmlElement.StartTag.Width - 2, _tokenTypes[0], _tokenModifiers[0]));
        //        // brackets
        //        _currentSemanticTokens.Add(new SemanticToken(line, offset, 1, _tokenTypes[3], _tokenModifiers[0]));
        //        _currentSemanticTokens.Add(new SemanticToken(line, offset + xmlElement.StartTag.Width - 1, 1, _tokenTypes[3], _tokenModifiers[0]));
        //    }
        //    // coloring end tag
        //    if (xmlElement.EndTag != null)
        //    {
        //        var (line, offset) = GetLineNumberAndOffsetByIndex(xmlElement.EndTag.SpanStart);
        //        _currentSemanticTokens.Add(new SemanticToken(line, offset + 2, xmlElement.EndTag.Width - 3, _tokenTypes[0], _tokenModifiers[0]));
        //        // brackets
        //        _currentSemanticTokens.Add(new SemanticToken(line, offset, 2, _tokenTypes[3], _tokenModifiers[0]));
        //        _currentSemanticTokens.Add(new SemanticToken(line, offset + xmlElement.EndTag.Width - 1, 1, _tokenTypes[3], _tokenModifiers[0]));
        //    }
        //}
    }
}
