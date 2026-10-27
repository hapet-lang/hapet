// This file is ported and adapted from the Akbura

using HapetFrontend.Entities;
using HapetFrontend.Syntax.Diagnostics;
using HapetFrontend.Syntax.Red;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace HapetFrontend.Syntax.Green
{
    internal sealed class WithThreeChildrenGreen : GreenSyntaxList
    {
        private readonly GreenNode _child0;
        private readonly GreenNode _child1;
        private readonly GreenNode _child2;

        public WithThreeChildrenGreen(GreenNode child0, GreenNode child1, GreenNode child2)
        {
            SlotCount = 3;

            _child0 = child0;
            _child1 = child1;
            _child2 = child2;

            var fullWidth = 0;
            ushort nodeFlags = 0;
            AdjustWidthAndFlags(child0, ref fullWidth, ref nodeFlags);
            AdjustWidthAndFlags(child1, ref fullWidth, ref nodeFlags);
            AdjustWidthAndFlags(child2, ref fullWidth, ref nodeFlags);

            FullWidth = fullWidth;
            Flags = nodeFlags;
        }

        public WithThreeChildrenGreen(ImmutableArray<HapetDiagnostic>? diagnostics, ImmutableArray<HapetSyntaxAnnotation>? annotations, GreenNode child0, GreenNode child1, GreenNode child2)
            : base(diagnostics, annotations)
        {
            SlotCount = 3;

            _child0 = child0;
            _child1 = child1;
            _child2 = child2;

            var fullWidth = 0;
            ushort nodeFlags = 0;
            AdjustWidthAndFlags(child0, ref fullWidth, ref nodeFlags);
            AdjustWidthAndFlags(child1, ref fullWidth, ref nodeFlags);
            AdjustWidthAndFlags(child2, ref fullWidth, ref nodeFlags);

            FullWidth = fullWidth;
            Flags = nodeFlags;
        }

        public override GreenNode GetSlot(int index)
        {
            return index switch
            {
                0 => _child0,
                1 => _child1,
                2 => _child2,
                _ => null,
            };
        }

        public override void CopyTo(ArrayElement<GreenNode>[] array, int offset)
        {
            array[offset].Value = _child0;
            array[offset + 1].Value = _child1;
            array[offset + 2].Value = _child2;
        }

        public override AstNode CreateRed(AstNode parent, int position)
        {
            // return new SyntaxList.WithThreeChildren(this, parent, position);
            throw new NotImplementedException(); // TODO:
        }

        public override GreenNode WithDiagnostics(ImmutableArray<HapetDiagnostic>? errors)
        {
            return new WithThreeChildrenGreen(errors, GetAnnotations(), _child0, _child1, _child2);
        }

        public override GreenNode WithAnnotations(ImmutableArray<HapetSyntaxAnnotation>? annotations)
        {
            return new WithThreeChildrenGreen(GetDiagnostics(), annotations, _child0, _child1, _child2);
        }
    }
}
