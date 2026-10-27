// This file is ported and adapted from the Akbura

using HapetFrontend.Entities;
using HapetFrontend.Extensions;
using HapetFrontend.Pools;
using HapetFrontend.Syntax.Diagnostics;
using HapetFrontend.Syntax.Red;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace HapetFrontend.Syntax.Green
{
    public abstract class GreenNode
    {
        [Flags]
        internal enum GreenNodeFlags : ushort
        {
            None = 0,
            ContainsDiagnostics = 1 << 9,
            ContainsAnnotations = 1 << 4,
            ContainsDiagnosticsDirectly = 1 << 5,
            ContainsAnnotationsDirectly = 1 << 6,
            IsNotMissing = 1 << 7,

            InheritMask = ContainsDiagnostics | ContainsAnnotations | IsNotMissing,
        }

        /// <summary>
        /// Width of the node
        /// </summary>
        private readonly int _fullWidth;
        public int FullWidth
        {
            get => _fullWidth;
            protected init => _fullWidth = value;
        }
        public virtual int Width => _fullWidth - GetLeadingTriviaWidth() - GetTrailingTriviaWidth();
        public virtual int GetLeadingTriviaWidth()
            => FullWidth != 0 ? GetFirstTerminal()?.GetLeadingTriviaWidth() ?? 0 : 0;
        public virtual int GetTrailingTriviaWidth()
            => FullWidth != 0 ? GetLastTerminal()?.GetTrailingTriviaWidth() ?? 0 : 0;
        public bool HasLeadingTrivia => GetLeadingTriviaWidth() != 0;
        public bool HasTrailingTrivia => GetTrailingTriviaWidth() != 0;


        /// <summary>
        /// Raw node kind to check without abstractions
        /// </summary>
        private readonly SyntaxKind _rawKind;
        public SyntaxKind Kind => _rawKind;
        public virtual SyntaxKind ContextualKind => Kind; // some kinds could be contextual
        public ushort RawKind => (ushort)_rawKind;


        /// <summary>
        /// Fast info storage
        /// </summary>
        private readonly ushort _nodeFlagsAndSlotCount;
        public const ushort SlotCountMask = 0b_0000_0000_0000_1111;
        public const ushort FlagsMask = 0b_1111_1111_1111_0000;
        public int SmallSlotCount => _nodeFlagsAndSlotCount & SlotCountMask;
        protected ushort Flags
        {
            get => (ushort)(_nodeFlagsAndSlotCount & FlagsMask);
            init
            {
                var existingSlots = (ushort)(_nodeFlagsAndSlotCount & SlotCountMask);
                _nodeFlagsAndSlotCount = (ushort)((value & FlagsMask) | existingSlots);
            }
        }
        public int SlotCount
        {
            get
            {
                if (SmallSlotCount < 15)
                {
                    return SmallSlotCount;
                }

                return GetSlotCount();
            }

            internal init
            {
                var slotCount = value > 14
                    ? (ushort)15
                    : (ushort)(value & 0b_0000_0000_0000_1111);

                _nodeFlagsAndSlotCount |= slotCount;
            }
        }

        private static readonly ConditionalWeakTable<GreenNode, HapetDiagnostic[]> _diagnosticsTable = new();
        private static readonly ConditionalWeakTable<GreenNode, HapetSyntaxAnnotation[]> _annotationsTable = new();


        internal protected ushort InheritMask => (ushort)(_nodeFlagsAndSlotCount & (ushort)GreenNodeFlags.InheritMask);

        public bool IsNotMissing
        {
            get => (_nodeFlagsAndSlotCount & (int)GreenNodeFlags.IsNotMissing) != 0;
            init => _nodeFlagsAndSlotCount = (ushort)(value ? (_nodeFlagsAndSlotCount | (int)GreenNodeFlags.IsNotMissing) : (_nodeFlagsAndSlotCount & ~(int)GreenNodeFlags.IsNotMissing));
        }

        public bool IsMissing
        {
            get => !IsNotMissing;
            init => IsNotMissing = !value;
        }

        public bool ContainsDiagnosticsDirectly
        {
            get => (_nodeFlagsAndSlotCount & (int)GreenNodeFlags.ContainsDiagnosticsDirectly) != 0;
            init => _nodeFlagsAndSlotCount = (ushort)(value ? (_nodeFlagsAndSlotCount | (int)GreenNodeFlags.ContainsDiagnosticsDirectly) : (_nodeFlagsAndSlotCount & ~(int)GreenNodeFlags.ContainsDiagnosticsDirectly));
        }

        public bool ContainsAnnotationsDirectly
        {
            get => (_nodeFlagsAndSlotCount & (int)GreenNodeFlags.ContainsAnnotationsDirectly) != 0;
            init => _nodeFlagsAndSlotCount = (ushort)(value ? (_nodeFlagsAndSlotCount | (int)GreenNodeFlags.ContainsAnnotationsDirectly) : (_nodeFlagsAndSlotCount & ~(int)GreenNodeFlags.ContainsAnnotationsDirectly));
        }

        public bool ContainsDiagnostics
        {
            get => (_nodeFlagsAndSlotCount & (int)GreenNodeFlags.ContainsDiagnostics) != 0;
            init => _nodeFlagsAndSlotCount = (ushort)(value
                ? (_nodeFlagsAndSlotCount | (int)GreenNodeFlags.ContainsDiagnostics)
                : (_nodeFlagsAndSlotCount & ~(int)GreenNodeFlags.ContainsDiagnostics));
        }

        public bool ContainsAnnotations
        {
            get => (_nodeFlagsAndSlotCount & (int)GreenNodeFlags.ContainsAnnotations) != 0;
            init => _nodeFlagsAndSlotCount = (ushort)(value
                ? (_nodeFlagsAndSlotCount | (int)GreenNodeFlags.ContainsAnnotations)
                : (_nodeFlagsAndSlotCount & ~(int)GreenNodeFlags.ContainsAnnotations));
        }

        internal bool IsList => _rawKind == SyntaxKind.ListNode;
        internal virtual bool IsToken => false;
        internal virtual bool IsTrivia => false;

        protected GreenNode(SyntaxKind kind, ImmutableArray<HapetDiagnostic>? diagnosticInfos, ImmutableArray<HapetSyntaxAnnotation>? annotations)
        {
            _rawKind = kind;

            var containsAnnotations = SetAnnotations(annotations);
            ContainsAnnotationsDirectly = containsAnnotations;
            ContainsAnnotations = containsAnnotations;

            var containsDiagnostics = SetDiagnostics(diagnosticInfos);
            ContainsDiagnosticsDirectly = containsDiagnostics;
            ContainsDiagnostics = containsDiagnostics;
        }

        public abstract GreenNode GetSlot(int index);
        public abstract AstNode CreateRed(AstNode parent, int position);

        /// <summary>
        /// The method should be overriden when there are more slots in child node
        /// </summary>
        /// <returns></returns>
        protected virtual int GetSlotCount()
        {
            return SmallSlotCount;
        }

        public virtual int GetSlotOffset(int index)
        {
            var offset = 0;
            for (var i = 0; i < index; i++)
            {
                var child = GetSlot(i);

                if (child != null)
                {
                    offset += child.FullWidth;
                }
            }

            return offset;
        }

        /// <summary>
        /// Find the slot that contains the given offset.
        /// </summary>
        /// <param name="offset">
        /// The target offset. Must be between 0 and <see cref="FullWidth"/>.
        /// </param>
        /// <returns>
        /// The slot index of the slot containing the given offset.
        /// </returns>
        /// <remarks>
        /// The base implementation is a linear search. This should be overridden
        /// if a derived class can implement it more efficiently.
        /// </remarks>
        public virtual int FindSlotIndexContainingOffset(int offset)
        {
            HapetAssert.Assert(0 <= offset && offset < FullWidth);

            int i;
            var accumulatedWidth = 0;

            for (i = 0; ; i++)
            {
                Debug.Assert(i < SlotCount);
                var child = GetSlot(i);
                if (child != null)
                {
                    accumulatedWidth += child.FullWidth;
                    if (offset < accumulatedWidth)
                    {
                        break;
                    }
                }
            }

            return i;
        }

        #region Diagnostics

        public GreenNode AddDiagnostics(params ImmutableArray<HapetDiagnostic> diagnosticInfos)
        {
            if (diagnosticInfos == null || diagnosticInfos.Length == 0)
            {
                return this;
            }

            var existingDiagnostics = GetDiagnostics();

            if (existingDiagnostics.Length == 0)
            {
                return WithDiagnostics(diagnosticInfos);
            }

            var allDiagnostics = new HapetDiagnostic[existingDiagnostics.Length + diagnosticInfos.Length];

            existingDiagnostics.CopyTo(allDiagnostics, 0);
            diagnosticInfos.CopyTo(allDiagnostics, existingDiagnostics.Length);

            var array = allDiagnostics.ToImmutableArrayUnsafe();

            return WithDiagnostics(array);
        }
        public GreenNode AddDiagnostics(IEnumerable<HapetDiagnostic> diagnosticInfos)
        {
            if (diagnosticInfos == null)
            {
                return this;
            }
            var existingDiagnostics = GetDiagnostics();
            if (existingDiagnostics.Length == 0)
            {
                return WithDiagnostics(diagnosticInfos.ToImmutableArray());
            }
            using var builder = ImmutableArrayBuilder<HapetDiagnostic>.Rent(existingDiagnostics.Length * 2);
            builder.AddRange(existingDiagnostics);
            builder.AddRange(diagnosticInfos);
            return WithDiagnostics(builder.ToImmutable());
        }

        public ImmutableArray<HapetDiagnostic> GetDiagnostics()
        {
            if (_diagnosticsTable.TryGetValue(this, out var diagnostics))
            {
                return diagnostics.ToImmutableArrayUnsafe();
            }

            return [];
        }

        private bool SetDiagnostics(ImmutableArray<HapetDiagnostic>? diagnostics)
        {
            if (!diagnostics.HasValue)
            {
                return false;
            }

            if (diagnostics.Value.Length == 0)
            {
                return false;
            }

            var value = diagnostics;
            var array = Unsafe.As<ImmutableArray<HapetDiagnostic>?, HapetDiagnostic[]>(ref value);

            _diagnosticsTable.Add(this, array);
            return true;
        }

        public abstract GreenNode WithDiagnostics(ImmutableArray<HapetDiagnostic>? diagnostics);

        #endregion

        #region Annotations

        public GreenNode AddAnnotations(params ImmutableArray<HapetSyntaxAnnotation> annotations)
        {
            if (annotations == null || annotations.Length == 0)
            {
                return this;
            }

            var existingAnnotations = GetAnnotations();

            if (existingAnnotations.Length == 0)
            {
                return WithAnnotations(annotations);
            }

            var allAnnotations = new HapetSyntaxAnnotation[existingAnnotations.Length + annotations.Length];

            existingAnnotations.CopyTo(allAnnotations, 0);
            annotations.CopyTo(allAnnotations, existingAnnotations.Length);

            var array = Unsafe.As<HapetSyntaxAnnotation[], ImmutableArray<HapetSyntaxAnnotation>>(ref allAnnotations);

            return WithAnnotations(array);
        }

        public GreenNode AddAnnotations(IEnumerable<HapetSyntaxAnnotation> annotations)
        {
            if (annotations == null)
            {
                return this;
            }
            var existingAnnotations = GetAnnotations();

            if (existingAnnotations.Length == 0)
            {
                return WithAnnotations(annotations.ToImmutableArray());
            }

            using var builder = ImmutableArrayBuilder<HapetSyntaxAnnotation>.Rent(existingAnnotations.Length * 2);

            builder.AddRange(existingAnnotations);
            builder.AddRange(annotations);

            return WithAnnotations(builder.ToImmutable());
        }

        public ImmutableArray<HapetSyntaxAnnotation> GetAnnotations()
        {
            if (_annotationsTable.TryGetValue(this, out var annotations))
            {
                return Unsafe.As<HapetSyntaxAnnotation[], ImmutableArray<HapetSyntaxAnnotation>>(ref annotations);
            }

            return [];
        }

        private bool SetAnnotations(ImmutableArray<HapetSyntaxAnnotation>? annotations)
        {
            if (!annotations.HasValue)
            {
                return false;
            }

            if (annotations.Value.Length == 0)
            {
                return false;
            }

            var value = annotations;
            var array = Unsafe.As<ImmutableArray<HapetSyntaxAnnotation>?, HapetSyntaxAnnotation[]>(ref value);

            _annotationsTable.Add(this, array);
            return true;
        }

        public bool HasAnnotations(SyntaxKind annotationKind)
        {
            var annotations = GetAnnotations();
            if (annotations == [])
            {
                return false;
            }

            foreach (var a in annotations)
            {
                if ((SyntaxKind)a.Kind == annotationKind)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAnnotations(IEnumerable<SyntaxKind> annotationKinds)
        {
            var annotations = GetAnnotations();
            if (annotations == [])
            {
                return false;
            }

            foreach (var a in annotations)
            {
                if (annotationKinds.Contains((SyntaxKind)a.Kind))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAnnotation([NotNullWhen(true)] HapetSyntaxAnnotation annotation)
        {
            var annotations = GetAnnotations();
            if (annotations == [])
            {
                return false;
            }

            if (annotation is null)
            {
                return false;
            }

            foreach (var a in annotations)
            {
                if (a == annotation)
                {
                    return true;
                }
            }

            return false;
        }

        public IEnumerable<HapetSyntaxAnnotation> GetAnnotations(SyntaxKind annotationKind)
        {
            var annotations = GetAnnotations();

            if (annotations == [])
            {
                return [];
            }

            return GetAnnotationsSlow(annotations, annotationKind);
        }

        public IEnumerable<HapetSyntaxAnnotation> GetAnnotations(IEnumerable<SyntaxKind> annotationKinds)
        {
            ArgumentNullException.ThrowIfNull(annotationKinds);

            var annotations = GetAnnotations();

            if (annotations == [])
            {
                return [];
            }

            return GetAnnotationsSlow(annotations, annotationKinds);
        }

        private static IEnumerable<HapetSyntaxAnnotation> GetAnnotationsSlow(ImmutableArray<HapetSyntaxAnnotation> annotations, SyntaxKind annotationKind)
        {
            foreach (var annotation in annotations)
            {
                if ((SyntaxKind)annotation.Kind == annotationKind)
                {
                    yield return annotation;
                }
            }
        }
        private static IEnumerable<HapetSyntaxAnnotation> GetAnnotationsSlow(ImmutableArray<HapetSyntaxAnnotation> annotations, IEnumerable<SyntaxKind> annotationKinds)
        {
            foreach (var annotation in annotations)
            {
                if (annotationKinds.Contains((SyntaxKind)annotation.Kind))
                {
                    yield return annotation;
                }
            }
        }

        public abstract GreenNode WithAnnotations(ImmutableArray<HapetSyntaxAnnotation>? annotations);

        public GreenNode WithoutAnnotations(IEnumerable<HapetSyntaxAnnotation> annotations)
        {
            var existingAnnotations = GetAnnotations();
            if (existingAnnotations == [])
            {
                return this;
            }

            using var toKeep = ImmutableArrayBuilder<HapetSyntaxAnnotation>.Rent(existingAnnotations.Length > 8
                ? existingAnnotations.Length * 2
                : 8);

            foreach (var annotation in existingAnnotations)
            {
                if (!annotations.Contains(annotation))
                {
                    toKeep.Add(annotation);
                }
            }

            if (toKeep.Count == existingAnnotations.Length)
            {
                return this;
            }

            return WithAnnotations(toKeep.ToImmutable());
        }

        #endregion

        #region Width and flags
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AdjustWidthAndFlags(GreenNode slot, ref int width, ref ushort flags)
        {
            width += slot.FullWidth;
            flags |= (ushort)(slot.Flags & slot.InheritMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AdjustWidthAndFlags(ReadOnlySpan<GreenNode> slots, ref int width, ref ushort flags)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];

                width += slot.FullWidth;
                flags |= (ushort)(slot.Flags | slot.InheritMask);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AdjustWidthAndFlags(ArrayElement<GreenNode>[] slots, ref int width, ref ushort flags)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i].Value;

                width += slot.FullWidth;
                flags |= (ushort)(slot.Flags | slot.InheritMask);
            }
        }
        #endregion

        #region Caching

        /// <summary>
        /// Maximum number of child nodes allowed for caching.
        /// Nodes exceeding this number of children are not eligible for caching.
        /// </summary>
        public const int MaxCachedChildNum = 3;

        /// <summary>
        /// Determines if the current node is eligible for caching based on its flags and slot count.
        /// Nodes are cacheable if they are not missing and their slot count does not exceed <see cref="MaxCachedChildNum"/>.
        /// </summary>
        public bool IsCacheable => IsNotMissing && SlotCount <= MaxCachedChildNum;

        /// <summary>
        /// Computes a unique hash code for caching the node, combining node flags, slot count, kind, and identities of child nodes.
        /// <para>
        /// The method uses <see cref="SmallSlotCount"/> directly to avoid virtual method calls, ensuring optimal performance.
        /// Since cacheable nodes always have a slot count within the small limit (<= <see cref="MaxCachedChildNum"/>),
        /// the use of <see cref="SmallSlotCount"/> is guaranteed to be safe here.
        /// </para>
        /// </summary>
        /// <returns>
        /// A positive integer representing the computed hash code.
        /// </returns>
        public int GetCacheHash()
        {
            HapetAssert.Assert(IsCacheable);

            var code = (_nodeFlagsAndSlotCount << 16) | (ushort)_rawKind;
            var count = SlotCount;

            for (var i = 0; i < count; i++)
            {
                var child = GetSlot(i);
                if (child != null)
                {
                    code = HashCode.Combine(RuntimeHelpers.GetHashCode(child), code);
                }
            }

            return code & int.MaxValue;
        }

        /// <summary>
        /// Checks if the current node is equivalent to another node in the cache,
        /// based on kind, combined flags and slot count, and one child node.
        /// </summary>
        public bool IsCacheEquivalent(SyntaxKind kind, ushort flagsAndSlotCount, GreenNode child1)
        {
            Debug.Assert(IsCacheable);

            return Kind == kind &&
                _nodeFlagsAndSlotCount == flagsAndSlotCount &&
                SmallSlotCount == 1 &&
                GetSlot(0) == child1;
        }

        /// <summary>
        /// Checks if the current node is equivalent to another node in the cache,
        /// based on kind, combined flags and slot count, and two child nodes.
        /// </summary>
        public bool IsCacheEquivalent(SyntaxKind kind, ushort flagsAndSlotCount, GreenNode child1, GreenNode child2)
        {
            Debug.Assert(IsCacheable);

            return Kind == kind &&
                _nodeFlagsAndSlotCount == flagsAndSlotCount &&
                SmallSlotCount == 2 &&
                GetSlot(0) == child1 &&
                GetSlot(1) == child2;
        }

        /// <summary>
        /// Checks if the current node is equivalent to another node in the cache,
        /// based on kind, combined flags and slot count, and three child nodes.
        /// </summary>
        public bool IsCacheEquivalent(SyntaxKind kind, ushort flagsAndSlotCount, GreenNode child1, GreenNode child2, GreenNode child3)
        {
            Debug.Assert(IsCacheable);

            return Kind == kind &&
                _nodeFlagsAndSlotCount == flagsAndSlotCount &&
                SmallSlotCount == 3 &&
                GetSlot(0) == child1 &&
                GetSlot(1) == child2 &&
                GetSlot(2) == child3;
        }

        #endregion

        #region Equivalence 
        public virtual bool IsEquivalentTo([NotNullWhen(true)] GreenNode other)
        {
            if (this == other)
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            return EquivalentToInternal(this, other);
        }

        private static bool EquivalentToInternal(GreenNode node1, GreenNode node2)
        {
            if (node1.Kind != node2.Kind)
            {
                // A single-element list is usually represented as just a single node,
                // but can be represented as a List node with one child. Move to that
                // child if necessary.
                if (node1.IsList && node1.SlotCount == 1)
                {
                    node1 = node1.GetSlot(0);
                }

                if (node2.IsList && node2.SlotCount == 1)
                {
                    node2 = node2.GetSlot(0);
                }

                if (node1 == null || node2 == null)
                {
                    return false;
                }

                if (node1.Kind != node2.Kind)
                {
                    return false;
                }
            }

            if (node1._fullWidth != node2._fullWidth)
            {
                return false;
            }

            var n = node1.SlotCount;
            if (n != node2.SlotCount)
            {
                return false;
            }

            for (var i = 0; i < n; i++)
            {
                var node1Child = node1.GetSlot(i);
                var node2Child = node2.GetSlot(i);
                if (node1Child != null && node2Child != null && !node1Child.IsEquivalentTo(node2Child))
                {
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Tokens 
        public virtual object GetValue() => null;
        public virtual string GetValueText() => string.Empty;
        public virtual GreenNode GetLeadingTrivia() => null;
        public virtual GreenNode GetTrailingTrivia() => null;

        public virtual GreenNode WithLeadingTrivia(GreenNode trivia) => this;
        public virtual GreenNode WithTrailingTrivia(GreenNode trivia) => this;

        public GreenNode GetFirstTerminal()
        {
            var node = this;

            do
            {
                GreenNode firstChild = null;
                for (int i = 0, n = node.SlotCount; i < n; i++)
                {
                    var child = node.GetSlot(i);
                    if (child != null)
                    {
                        firstChild = child;
                        break;
                    }
                }
                node = firstChild;
            }
            while (node?.SmallSlotCount > 0);

            return node;
        }

        public GreenNode GetLastTerminal()
        {
            var node = this;

            do
            {
                GreenNode lastChild = null;
                for (var i = node.SlotCount - 1; i >= 0; i--)
                {
                    var child = node.GetSlot(i);
                    if (child != null)
                    {
                        lastChild = child;
                        break;
                    }
                }
                node = lastChild;
            }
            // Note: it's ok to examine SmallSlotCount here.  All we're trying to do is make sure we have at least one
            // child.  And SmallSlotCount works both for small counts and large counts.  This avoids an unnecessary
            // virtual call for large list nodes.
            while (node?.SmallSlotCount > 0);

            return node;
        }

        public GreenNode GetLastNonmissingTerminal()
        {
            var node = this;

            do
            {
                GreenNode nonmissingChild = null;
                for (var i = node.SlotCount - 1; i >= 0; i--)
                {
                    var child = node.GetSlot(i);
                    if (child != null && child.IsNotMissing)
                    {
                        nonmissingChild = child;
                        break;
                    }
                }
                node = nonmissingChild;
            }
            // Note: it's ok to examine SmallSlotCount here.  All we're trying to do is make sure we have at least one
            // child.  And SmallSlotCount works both for small counts and large counts.  This avoids an unnecessary
            // virtual call for large list nodes.
            while (node?.SmallSlotCount > 0);

            return node;
        }
        #endregion
    }
}
