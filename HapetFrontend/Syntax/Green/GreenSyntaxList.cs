// This file is ported and adapted from the Akbura

using HapetFrontend.Entities;
using HapetFrontend.Pools;
using HapetFrontend.Syntax.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace HapetFrontend.Syntax.Green
{
    internal abstract partial class GreenSyntaxList : GreenNode
    {
        public GreenSyntaxList(ImmutableArray<HapetDiagnostic>? diagnostics, ImmutableArray<HapetSyntaxAnnotation>? annotations) : base(SyntaxKind.ListNode, diagnostics, annotations)
        {
        }

        public GreenSyntaxList() : base(SyntaxKind.ListNode, null, null)
        {
        }

        public static GreenNode List(GreenNode child)
        {
            return child;
        }

        public static WithTwoChildrenGreen List(GreenNode child0, GreenNode child1)
        {
            HapetAssert.Assert(child0 != null);
            HapetAssert.Assert(child1 != null);

            var cached = GreenNodeCache.TryGetNode(SyntaxKind.ListNode, child0, child1, out var hash);

            if (cached != null)
            {
                return (WithTwoChildrenGreen)cached;
            }

            var result = new WithTwoChildrenGreen(child0, child1);
            if (hash >= 0)
            {
                GreenNodeCache.AddNode(result, hash);
            }

            return result;
        }

        public static WithThreeChildrenGreen List(GreenNode child0, GreenNode child1, GreenNode child2)
        {
            HapetAssert.Assert(child0 != null);
            HapetAssert.Assert(child1 != null);
            HapetAssert.Assert(child2 != null);

            var cached = GreenNodeCache.TryGetNode(SyntaxKind.ListNode, child0, child1, child2, out var hash);

            if (cached != null)
            {
                return (WithThreeChildrenGreen)cached;
            }

            var result = new WithThreeChildrenGreen(child0, child1, child2);

            if (hash >= 0)
            {
                GreenNodeCache.AddNode(result, hash);
            }

            return result;
        }

        public static GreenNode List(GreenNode[] nodes)
        {
            return List(nodes, nodes.Length);
        }

        public static GreenNode List(GreenNode[] nodes, int count)
        {
            var array = new ArrayElement<GreenNode>[count];
            for (var i = 0; i < count; i++)
            {
                var node = nodes[i];
                HapetAssert.Assert(node is not null);
                array[i].Value = node;
            }

            return List(array);
        }

        public static GreenSyntaxList List(ReadOnlySpan<GreenNode> nodes)
        {
            return List(nodes, nodes.Length);
        }

        public static GreenSyntaxList List(ReadOnlySpan<GreenNode> nodes, int count)
        {
            var array = new ArrayElement<GreenNode>[count];
            for (var i = 0; i < count; i++)
            {
                var node = nodes[i];
                HapetAssert.Assert(node is not null);
                array[i].Value = node;
            }

            return List(array);
        }

        internal static GreenSyntaxList List(ArrayElement<GreenNode>[] children)
        {
            // "WithLotsOfChildren" list will allocate a separate array to hold
            // precomputed node offsets. It may not be worth it for smallish lists.
            if (children.Length < 10)
            {
                return new WithManyChildrenGreen(children);
            }
            else
            {
                return new WithLotsOfChildrenGreen(children);
            }
        }

        public abstract void CopyTo(ArrayElement<GreenNode>[] array, int offset);

        public static GreenNode Concat(GreenNode left, GreenNode right)
        {
            if (left == null)
            {
                return right;
            }

            if (right == null)
            {
                return left;
            }

            if (left is GreenSyntaxList leftList)
            {
                if (right is GreenSyntaxList rightList)
                {
                    var tmp = new ArrayElement<GreenNode>[left.SlotCount + right.SlotCount];
                    leftList.CopyTo(tmp, 0);
                    rightList.CopyTo(tmp, left.SlotCount);
                    return List(tmp);
                }
                else
                {
                    var tmp = new ArrayElement<GreenNode>[left.SlotCount + 1];
                    leftList.CopyTo(tmp, 0);
                    tmp[left.SlotCount].Value = right;
                    return List(tmp);
                }
            }
            else if (right is GreenSyntaxList rightList)
            {
                var tmp = new ArrayElement<GreenNode>[rightList.SlotCount + 1];
                tmp[0].Value = left;
                rightList.CopyTo(tmp, 1);
                return List(tmp);
            }
            else
            {
                return List(left, right);
            }
        }
    }
}
