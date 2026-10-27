using HapetFrontend.Syntax.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace HapetFrontend.Syntax.Green.Tokens
{
    internal class SyntaxIdentifier : GreenSyntaxToken
    {
        protected readonly string TextField;

        public SyntaxIdentifier(string text)
            : base(SyntaxKind.Identifier)
        {
            TextField = text;
            FullWidth = text.Length;
        }

        public SyntaxIdentifier(
            string text,
            ImmutableArray<HapetDiagnostic>? diagnostics,
            ImmutableArray<HapetSyntaxAnnotation>? annotations)
            : base(SyntaxKind.Identifier, diagnostics, annotations)
        {
            TextField = text;
            FullWidth = text.Length;
        }

        public override string Text => TextField;

        public override object Value => TextField;

        public override string ValueText => TextField;

        public override GreenSyntaxToken TokenWithLeadingTrivia(GreenNode trivia)
        {
            return new SyntaxIdentifierWithTrivia(
                Kind,
                TextField,
                TextField,
                trivia,
                null,
                GetDiagnostics(),
                GetAnnotations());
        }

        public override GreenSyntaxToken TokenWithTrailingTrivia(GreenNode trivia)
        {
            return new SyntaxIdentifierWithTrivia(
                Kind,
                TextField,
                TextField,
                null,
                trivia,
                GetDiagnostics(),
                GetAnnotations());
        }

        public override GreenNode WithDiagnostics(ImmutableArray<HapetDiagnostic>? diagnostics)
        {
            return new SyntaxIdentifier(Text, diagnostics, GetAnnotations());
        }

        public override GreenNode WithAnnotations(ImmutableArray<HapetSyntaxAnnotation>? annotations)
        {
            return new SyntaxIdentifier(Text, GetDiagnostics(), annotations);
        }
    }
}
