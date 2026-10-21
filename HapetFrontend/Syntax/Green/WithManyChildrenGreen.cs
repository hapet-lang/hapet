using HapetFrontend.Entities;
using HapetFrontend.Extensions;
using HapetFrontend.Syntax.Diagnostics;
using HapetFrontend.Syntax.Red;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace HapetFrontend.Syntax.Green
{
    public abstract class WithManyChildrenBaseGreen : GreenSyntaxList
    {
        public readonly ArrayElement<GreenNode>[] _children;

        public WithManyChildrenBaseGreen(ArrayElement<GreenNode>[] children)
        {
            _children = children;
            SlotCount = children.Length;

            var fullWidth = 0;
            ushort nodeAndFlags = 0;
            AdjustWidthAndFlags(children, ref fullWidth, ref nodeAndFlags);

            FullWidth = fullWidth;
            Flags = nodeAndFlags;
        }

        public WithManyChildrenBaseGreen(ImmutableArray<HapetDiagnostic>? diagnostics, ImmutableArray<HapetSyntaxAnnotation>? annotations, ArrayElement<GreenNode>[] children)
                : base(diagnostics, annotations)
        {
            _children = children;
            SlotCount = children.Length;

            var fullWidth = 0;
            ushort nodeFlags = 0;
            AdjustWidthAndFlags(children, ref fullWidth, ref nodeFlags);

            FullWidth = fullWidth;
            Flags = nodeFlags;
        }

        public sealed override void CopyTo(ArrayElement<GreenNode>[] array, int offset)
        {
            Array.Copy(_children, 0, array, offset, _children.Length);
        }

        public sealed override AstNode CreateRed(AstNode parent, int position)
        {
            var separated = SlotCount > 1 && HasNodeTokenPattern();

            /*
            return separated
                    ? new SyntaxList.SeparatedWithManyChildren(this, parent, position)
                    : new SyntaxList.WithManyChildren(this, parent, position);
            */
            throw new NotImplementedException(); // TODO:
        }

        private bool HasNodeTokenPattern()
        {
            for (var i = 0; i < SlotCount; i++)
            {
                // even slots must not be tokens, odds slots must be tokens
                if (GetSlot(i)!.IsToken == ((i & 1) == 0))
                {
                    return false;
                }
            }

            return true;
        }

        public override GreenNode GetSlot(int index) => _children[index].Value;

        protected override int GetSlotCount()
        {
            return _children.Length;
        }
    }

    internal sealed class WithManyChildrenGreen : WithManyChildrenBaseGreen
    {
        public WithManyChildrenGreen(ArrayElement<GreenNode>[] children) : base(children)
        {
        }

        public WithManyChildrenGreen(ImmutableArray<HapetDiagnostic>? diagnostics, ImmutableArray<HapetSyntaxAnnotation>? annotations, ArrayElement<GreenNode>[] children)
                : base(diagnostics, annotations, children)
        {
        }

        public override GreenNode WithAnnotations(ImmutableArray<HapetSyntaxAnnotation>? annotations)
        {
            return new WithManyChildrenGreen(GetDiagnostics(), annotations, _children);
        }

        public override GreenNode WithDiagnostics(ImmutableArray<HapetDiagnostic>? diagnostics)
        {
            return new WithManyChildrenGreen(diagnostics, GetAnnotations(), _children);
        }
    }

    internal sealed class WithLotsOfChildrenGreen : WithManyChildrenBaseGreen
    {
        private readonly int[] _childOffsets;

        public WithLotsOfChildrenGreen(ArrayElement<GreenNode>[] children) : base(children)
        {
            _childOffsets = CalculateOffsets(children);
        }

        public WithLotsOfChildrenGreen(ImmutableArray<HapetDiagnostic>? diagnostics, ImmutableArray<HapetSyntaxAnnotation>? annotations, ArrayElement<GreenNode>[] children)
            : base(diagnostics, annotations, children)
        {
            _childOffsets = CalculateOffsets(children);
        }

        public override int GetSlotOffset(int index)
        {
            return _childOffsets[index];
        }

        /// <summary>
        /// Find the slot that contains the given offset.
        /// </summary>
        /// <param name="offset">
        /// The target offset. Must be between 0 and <see cref="GreenNode.FullWidth"/>.
        /// </param>
        /// <returns>
        /// The slot index of the slot containing the given offset.
        /// </returns>
        /// <remarks>
        /// This implementation uses a binary search to find the first slot that contains
        /// the given offset.
        /// </remarks>
        public override int FindSlotIndexContainingOffset(int offset)
        {
            Debug.Assert(offset >= 0 && offset < FullWidth);
            return _childOffsets.BinarySearchUpperBound(offset) - 1;
        }

        private static int[] CalculateOffsets(ArrayElement<GreenNode>[] children)
        {
            var n = children.Length;
            var childOffsets = new int[n];
            var offset = 0;
            for (var i = 0; i < n; i++)
            {
                childOffsets[i] = offset;
                offset += children[i].Value.FullWidth;
            }
            return childOffsets;
        }

        public override GreenNode WithAnnotations(ImmutableArray<HapetSyntaxAnnotation>? annotations)
        {
            return new WithLotsOfChildrenGreen(GetDiagnostics(), annotations, _children);
        }

        public override GreenNode WithDiagnostics(ImmutableArray<HapetDiagnostic>? diagnostics)
        {
            return new WithLotsOfChildrenGreen(diagnostics, GetAnnotations(), _children);
        }
    }
}
