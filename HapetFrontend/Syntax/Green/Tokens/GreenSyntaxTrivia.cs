using HapetFrontend.Syntax.Diagnostics;
using HapetFrontend.Syntax.Red;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace HapetFrontend.Syntax.Green.Tokens
{
    internal partial class GreenSyntaxTrivia : GreenNode
    {
        public readonly string Text;

        public GreenSyntaxTrivia(
            SyntaxKind kind,
            string text,
            ImmutableArray<HapetDiagnostic>? diagnostics = null,
            ImmutableArray<HapetSyntaxAnnotation>? annotations = null)
            : base(kind, diagnostics, annotations)
        {
            Text = text;
            FullWidth = text.Length;
        }

        internal override bool IsTrivia => true;

        public static GreenSyntaxTrivia Create(SyntaxKind kind, string text)
        {
            return new GreenSyntaxTrivia(kind, text);
        }

        public override string ToString()
        {
            return Text;
        }

        public override GreenNode GetSlot(int index)
        {
            throw new UnreachableException();
        }

        public override int Width
        {
            get
            {
                Debug.Assert(FullWidth == Text.Length);
                return FullWidth;
            }
        }

        public override int GetLeadingTriviaWidth() => 0;
        public override int GetTrailingTriviaWidth() => 0;

        public override GreenNode WithDiagnostics(ImmutableArray<HapetDiagnostic>? diagnostics)
        {
            return new GreenSyntaxTrivia(Kind, Text, diagnostics, GetAnnotations());
        }

        public override GreenNode WithAnnotations(ImmutableArray<HapetSyntaxAnnotation>? annotations)
        {
            return new GreenSyntaxTrivia(Kind, Text, GetDiagnostics(), annotations);
        }

        /* TODO:
        protected override void WriteTriviaTo(TextWriter writer)
        {
            writer.Write(Text);
        }
        */

        public override bool IsEquivalentTo(GreenNode other)
        {
            if (!base.IsEquivalentTo(other))
            {
                return false;
            }

            return Text == ((GreenSyntaxTrivia)other).Text;
        }

        public override AstNode CreateRed(AstNode parent, int position)
        {
            // return ThrowHelper.ThrowUnreachableException<AkburaSyntax>(); // TODO:
            throw new NotImplementedException();
        }
    }
}
