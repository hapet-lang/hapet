using HapetFrontend.Entities;
using HapetFrontend.Syntax.Diagnostics;
using HapetFrontend.Syntax.Red;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace HapetFrontend.Syntax.Green.Tokens
{
    internal partial class GreenSyntaxToken : GreenNode
    {
        public GreenSyntaxToken(SyntaxKind kind, ImmutableArray<HapetDiagnostic>? diagnostics, ImmutableArray<HapetSyntaxAnnotation>? annotations) : base(kind, diagnostics, annotations)
        {
            FullWidth = Text?.Length ?? 0;
            IsMissing = false;
        }

        public GreenSyntaxToken(SyntaxKind kind) : this(kind, null, null)
        {
            FullWidth = Text?.Length ?? 0;
            IsMissing = false;
        }

        /// <summary>
        /// Indicates whether this is a token.
        /// </summary>
        internal override bool IsToken => true;

        /// <summary>
        /// Gets the text representation of the token.
        /// </summary>
        public virtual string Text => SyntaxFacts.GetText(Kind);

        /// <summary>
        /// Gets the width of the token.
        /// </summary>
        public override int Width => Text.Length;

        /// <summary>
        /// Gets the value text of the token.
        /// </summary>
        public virtual string ValueText => Text;

        /// <summary>
        /// Gets the value associated with the token based on its kind.
        /// </summary>
        public virtual object Value => SyntaxFacts.GetValue(Kind, Text);

        /// <summary>
        /// Retrieves a specific slot of this syntax token.
        /// </summary>
        public override GreenNode GetSlot(int index)
        {
            throw new UnreachableException();
        }

        /// <summary>
        /// Creates a red node representation.
        /// </summary>
        public override AstNode CreateRed(AstNode parent, int position)
        {
            throw new UnreachableException();
        }

        public override GreenNode WithAnnotations(ImmutableArray<HapetSyntaxAnnotation>? annotations)
        {
            return new GreenSyntaxToken(Kind, GetDiagnostics(), annotations);
        }

        public override GreenNode WithDiagnostics(ImmutableArray<HapetDiagnostic>? diagnostics)
        {
            return new GreenSyntaxToken(Kind, diagnostics, GetAnnotations());
        }

        /// <summary>
        /// Creates a syntax token.
        /// </summary>
        public static GreenSyntaxToken Create(SyntaxKind kind)
        {
            if (kind > LastTokenWithWellKnownText)
            {
                return CreateMissing(kind);
            }

            return _tokensWithNoTrivia[(int)kind].Value;
        }

        /// <summary>
        /// Creates a syntax token with trivia.
        /// </summary>
        public static GreenSyntaxToken Create(SyntaxKind kind, GreenNode leading, GreenNode trailing)
        {
            if (kind > LastTokenWithWellKnownText)
            {
                return CreateMissing(kind, leading, trailing);
            }

            if (leading == null)
            {
                if (trailing == null)
                {
                    return _tokensWithNoTrivia[(int)kind].Value;
                }
                else if (trailing == GreenSyntaxFactory.Space)
                {
                    return _tokensWithSingleTrailingSpace[(int)kind].Value;
                }
                else if (trailing == GreenSyntaxFactory.CarriageReturnLineFeed)
                {
                    return _tokensWithSingleTrailingCRLF[(int)kind].Value;
                }
            }

            if (leading == GreenSyntaxFactory.ElasticZeroSpace && trailing == GreenSyntaxFactory.ElasticZeroSpace)
            {
                return _tokensWithElasticTrivia[(int)kind].Value;
            }

            return new SyntaxTokenWithTrivia(kind, leading, trailing);
        }

        public static GreenSyntaxToken CreateTokenWithTrailingSpace(SyntaxKind kind)
        {
            if (kind > LastTokenWithWellKnownText)
            {
                return CreateMissing(kind, null, GreenSyntaxFactory.Space);
            }

            return _tokensWithSingleTrailingSpace[(ushort)kind];
        }

        /// <summary>
        /// Creates a missing token.
        /// </summary>
        public static GreenSyntaxToken CreateMissing(SyntaxKind kind)
        {
            if (kind <= LastTokenWithWellKnownText)
            {
                return _missingTokensWithNoTrivia[(int)kind].Value;
            }
            else if (kind == SyntaxKind.Identifier)
            {
                return _missingIdentifierTokenWithNoTrivia;
            }

            return new MissingTokenWithTrivia(kind, leading: null, trailing: null);
        }

        /// <summary>
        /// Creates a missing token with trivia.
        /// </summary>
        public static GreenSyntaxToken CreateMissing(SyntaxKind kind, GreenNode leading, GreenNode trailing)
        {
            return new MissingTokenWithTrivia(kind, leading, trailing);
        }

        /// <summary>
        /// Creates a token with a value.
        /// </summary>
        public static GreenSyntaxToken WithValue<T>(SyntaxKind kind, string text, T value)
        {
            return new SyntaxTokenWithValue<T>(kind, text, value);
        }

        /// <summary>
        /// Creates a token with a value and trivia.
        /// </summary>
        public static GreenSyntaxToken WithValue<T>(SyntaxKind kind, GreenNode leading, string text, T value, GreenNode trailing)
        {
            return new SyntaxTokenWithValueAndTrivia<T>(kind, text, value, leading, trailing);
        }

        /// <summary>
        /// Gets the leading trivia.
        /// </summary>
        public GreenNode LeadingTrivia => GreenSyntaxList.List(GetLeadingTrivia());

        /// <summary>
        /// Gets the trailing trivia.
        /// </summary>
        public GreenNode TrailingTrivia => GreenSyntaxList.List(GetTrailingTrivia());

        /// <summary>
        /// Creates a new token with leading trivia.
        /// </summary>
        public sealed override GreenNode WithLeadingTrivia(GreenNode trivia)
        {
            return trivia is not GreenNode green ? this : (GreenNode)TokenWithLeadingTrivia(green);
        }

        /// <summary>
        /// Creates a new token with leading trivia.
        /// </summary>
        public virtual GreenSyntaxToken TokenWithLeadingTrivia(GreenNode trivia)
        {
            return new SyntaxTokenWithTrivia(Kind, trivia, null, GetDiagnostics(), GetAnnotations());
        }

        /// <summary>
        /// Creates a new token with trailing trivia.
        /// </summary>
        public sealed override GreenNode WithTrailingTrivia(GreenNode trivia)
        {
            return trivia is not GreenNode green ? this : (GreenNode)TokenWithTrailingTrivia(green);
        }

        /// <summary>
        /// Creates a new token with trailing trivia.
        /// </summary>
        public virtual GreenSyntaxToken TokenWithTrailingTrivia(GreenNode trivia)
        {
            return new SyntaxTokenWithTrivia(Kind, null, trivia, GetDiagnostics(), GetAnnotations());
        }

        /// <summary>
        /// Defines the range of well-known tokens.
        /// </summary>
        public const SyntaxKind FirstTokenWithWellKnownText = SyntaxKind.FirstTokenWithWellKnownText;
        public const SyntaxKind LastTokenWithWellKnownText = SyntaxKind.LastTokenWithWellKnownText;

        /// <summary>
        /// Predefined token arrays.
        /// </summary>
        private static readonly ArrayElement<GreenSyntaxToken>[] _tokensWithNoTrivia = new ArrayElement<GreenSyntaxToken>[(int)LastTokenWithWellKnownText + 1];
        private static readonly ArrayElement<GreenSyntaxToken>[] _tokensWithSingleTrailingSpace = new ArrayElement<GreenSyntaxToken>[(int)LastTokenWithWellKnownText + 1];
        private static readonly ArrayElement<GreenSyntaxToken>[] _tokensWithElasticTrivia = new ArrayElement<GreenSyntaxToken>[(int)LastTokenWithWellKnownText + 1];
        private static readonly ArrayElement<GreenSyntaxToken>[] _tokensWithSingleTrailingCRLF = new ArrayElement<GreenSyntaxToken>[(int)LastTokenWithWellKnownText + 1];
        private static readonly ArrayElement<GreenSyntaxToken>[] _missingTokensWithNoTrivia = new ArrayElement<GreenSyntaxToken>[(int)LastTokenWithWellKnownText + 1];
        private static readonly GreenSyntaxToken _missingIdentifierTokenWithNoTrivia = new MissingTokenWithTrivia(SyntaxKind.Identifier, leading: null, trailing: null);

        /// <summary>
        /// Static constructor initializing predefined tokens.
        /// </summary>
        static GreenSyntaxToken()
        {
            for (var kind = FirstTokenWithWellKnownText; kind <= LastTokenWithWellKnownText; kind++)
            {
                _tokensWithNoTrivia[(int)kind].Value = new GreenSyntaxToken(kind);
                _tokensWithElasticTrivia[(int)kind].Value = new SyntaxTokenWithTrivia(kind, GreenSyntaxFactory.ElasticZeroSpace, GreenSyntaxFactory.ElasticZeroSpace);
                _tokensWithSingleTrailingSpace[(int)kind].Value = new SyntaxTokenWithTrivia(kind, null, GreenSyntaxFactory.Space);
                _tokensWithSingleTrailingCRLF[(int)kind].Value = new SyntaxTokenWithTrivia(kind, null, GreenSyntaxFactory.CarriageReturnLineFeed);
                _missingTokensWithNoTrivia[(int)kind].Value = new MissingTokenWithTrivia(kind, leading: null, trailing: null);
            }
        }

        /// <summary>
        /// Returns all well-known tokens.
        /// </summary>
        public static IEnumerable<GreenSyntaxToken> GetWellKnownTokens()
        {
            foreach (var element in _tokensWithNoTrivia)
            {
                if (element.Value != null)
                {
                    yield return element.Value;
                }
            }
            foreach (var element in _tokensWithSingleTrailingSpace)
            {
                if (element.Value != null)
                {
                    yield return element.Value;
                }
            }
            foreach (var element in _tokensWithSingleTrailingCRLF)
            {
                if (element.Value != null)
                {
                    yield return element.Value;
                }
            }
        }

        public static GreenSyntaxToken Identifier(string text)
        {
            return new SyntaxIdentifier(text);
        }

        public static GreenSyntaxToken Identifier(GreenNode leading, string text, GreenNode trailing)
        {
            if (leading == null)
            {
                if (trailing == null)
                {
                    return Identifier(text);
                }
                else
                {
                    return new SyntaxIdentifierWithTrailingTrivia(text, trailing);
                }
            }
            return new SyntaxIdentifierWithTrivia(SyntaxKind.Identifier, text, text, leading, trailing);
        }

        public static GreenSyntaxToken Identifier(SyntaxKind contextualKind, GreenNode leading, string text, string valueText, GreenNode trailing)
        {
            if (contextualKind == SyntaxKind.Identifier && valueText == text)
            {
                return Identifier(leading, text, trailing);
            }
            return new SyntaxIdentifierWithTrivia(contextualKind, text, valueText, leading, trailing);
        }

        public override string ToString()
        {
            return Text;
        }

        public override int GetLeadingTriviaWidth()
        {
            var leading = GetLeadingTrivia();
            return leading != null ? leading.FullWidth : 0;
        }

        public override int GetTrailingTriviaWidth()
        {
            var trailing = GetTrailingTrivia();
            return trailing != null ? trailing.FullWidth : 0;
        }

        public override bool IsEquivalentTo(GreenNode other)
        {
            if (!base.IsEquivalentTo(other))
            {
                return false;
            }

            var otherToken = (GreenSyntaxToken)other;

            if (this.Text != otherToken.Text)
            {
                return false;
            }

            var thisLeading = this.GetLeadingTrivia();
            var otherLeading = otherToken.GetLeadingTrivia();
            if (thisLeading != otherLeading)
            {
                if (thisLeading == null || otherLeading == null)
                {
                    return false;
                }

                if (!thisLeading.IsEquivalentTo(otherLeading))
                {
                    return false;
                }
            }

            var thisTrailing = this.GetTrailingTrivia();
            var otherTrailing = otherToken.GetTrailingTrivia();
            if (thisTrailing != otherTrailing)
            {
                if (thisTrailing == null || otherTrailing == null)
                {
                    return false;
                }

                if (!thisTrailing.IsEquivalentTo(otherTrailing))
                {
                    return false;
                }
            }

            return true;
        }

        //protected override void WriteTokenTo(TextWriter writer, bool leading, bool trailing)
        //{
        //    if (leading)
        //    {
        //        var trivia = GetLeadingTrivia();
        //        trivia?.WriteTo(writer, true, true);
        //    }

        //    writer.Write(Text);

        //    if (trailing)
        //    {
        //        var trivia = GetTrailingTrivia();
        //        trivia?.WriteTo(writer, true, true);
        //    }
        //}
    }
}
