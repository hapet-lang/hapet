using HapetFrontend.Syntax.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace HapetFrontend.Syntax.Green.Tokens
{
    internal sealed class MissingTokenWithTrivia : SyntaxTokenWithTrivia
    {
        public MissingTokenWithTrivia(SyntaxKind kind, GreenNode leading, GreenNode trailing)
            : base(kind, leading, trailing)
        {
            IsMissing = true;
        }

        public MissingTokenWithTrivia(SyntaxKind kind, GreenNode leading, GreenNode trailing, ImmutableArray<HapetDiagnostic>? diagnostics, ImmutableArray<HapetSyntaxAnnotation>? annotations)
            : base(kind, leading, trailing, diagnostics, annotations)
        {
            IsMissing = true;
        }

        public override string Text => string.Empty;

        public override object Value => Kind switch
        {
            SyntaxKind.Identifier => string.Empty,
            _ => null,
        };

        public override GreenSyntaxToken TokenWithLeadingTrivia(GreenNode trivia)
        {
            return new MissingTokenWithTrivia(Kind, trivia, TrailingField, GetDiagnostics(), GetAnnotations());
        }

        public override GreenSyntaxToken TokenWithTrailingTrivia(GreenNode trivia)
        {
            return new MissingTokenWithTrivia(Kind, LeadingField, trivia, GetDiagnostics(), GetAnnotations());
        }

        public override GreenNode WithDiagnostics(ImmutableArray<HapetDiagnostic>? diagnostics)
        {
            return new MissingTokenWithTrivia(Kind, LeadingField, TrailingField, diagnostics, GetAnnotations());
        }

        public override GreenNode WithAnnotations(ImmutableArray<HapetSyntaxAnnotation>? annotations)
        {
            return new MissingTokenWithTrivia(Kind, LeadingField, TrailingField, GetDiagnostics(), annotations);
        }
    }
}
