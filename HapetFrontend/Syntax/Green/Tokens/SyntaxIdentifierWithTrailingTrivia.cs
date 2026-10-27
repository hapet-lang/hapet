using HapetFrontend.Syntax.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace HapetFrontend.Syntax.Green.Tokens
{
    internal class SyntaxIdentifierWithTrailingTrivia : SyntaxIdentifier
    {
        private readonly GreenNode _trailing;

        public SyntaxIdentifierWithTrailingTrivia(string text, GreenNode trailing)
            : base(text)
        {
            var fullWidth = FullWidth;
            var flags = Flags;

            if (trailing != null)
            {
                AdjustWidthAndFlags(trailing, ref fullWidth, ref flags);
                _trailing = trailing;
            }

            FullWidth = fullWidth;
            Flags = flags;
        }

        public SyntaxIdentifierWithTrailingTrivia(
            string text,
            GreenNode trailing,
            ImmutableArray<HapetDiagnostic>? diagnostics,
            ImmutableArray<HapetSyntaxAnnotation>? annotations)
            : base(text, diagnostics, annotations)
        {
            var fullWidth = FullWidth;
            var flags = Flags;

            if (trailing != null)
            {
                AdjustWidthAndFlags(trailing, ref fullWidth, ref flags);
                _trailing = trailing;
            }

            FullWidth = fullWidth;
            Flags = flags;
        }

        public override GreenNode GetTrailingTrivia()
        {
            return _trailing;
        }

        public override GreenSyntaxToken TokenWithLeadingTrivia(GreenNode trivia)
        {
            return new SyntaxIdentifierWithTrivia(
                Kind,
                TextField,
                TextField,
                trivia,
                _trailing,
                GetDiagnostics(),
                GetAnnotations());
        }

        public override GreenSyntaxToken TokenWithTrailingTrivia(GreenNode trivia)
        {
            return new SyntaxIdentifierWithTrailingTrivia(
                TextField,
                trivia,
                GetDiagnostics(),
                GetAnnotations());
        }

        public override GreenNode WithDiagnostics(ImmutableArray<HapetDiagnostic>? diagnostics)
        {
            return new SyntaxIdentifierWithTrailingTrivia(
                TextField,
                _trailing,
                diagnostics,
                GetAnnotations());
        }

        public override GreenNode WithAnnotations(ImmutableArray<HapetSyntaxAnnotation>? annotations)
        {
            return new SyntaxIdentifierWithTrailingTrivia(
                TextField,
                _trailing,
                GetDiagnostics(),
                annotations);
        }
    }
}
