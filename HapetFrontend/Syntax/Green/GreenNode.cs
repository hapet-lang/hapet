
using HapetFrontend.Extensions;
using HapetFrontend.Pools;
using HapetFrontend.Syntax.Diagnostics;
using HapetFrontend.Syntax.Red;
using System.Collections.Immutable;
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
        /// <summary>
        /// Fast info storage
        /// </summary>
        private readonly ushort _nodeFlagsAndSlotCount;
        /// <summary>
        /// Raw node kind to check without abstractions
        /// </summary>
        private readonly SyntaxKind _rawKind;

        public int FullWidth
        {
            get => _fullWidth;
            protected init => _fullWidth = value;
        }

        public SyntaxKind Kind => _rawKind;
        public ushort RawKind => (ushort)_rawKind;

        public int SmallSlotCount => _nodeFlagsAndSlotCount & 0b_0000_0000_0000_1111;

        private static readonly ConditionalWeakTable<GreenNode, HapetDiagnostic[]> _diagnosticsTable = new();
        private static readonly ConditionalWeakTable<GreenNode, HapetSyntaxAnnotation[]> _annotationsTable = new();

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

        public virtual int Width => _fullWidth - GetLeadingTriviaWidth() - GetTrailingTriviaWidth();

        public virtual int GetLeadingTriviaWidth()
            => FullWidth != 0 ? GetFirstTerminal()?.GetLeadingTriviaWidth() ?? 0 : 0;

        public virtual int GetTrailingTriviaWidth()
            => FullWidth != 0 ? GetLastTerminal()?.GetTrailingTriviaWidth() ?? 0 : 0;

        public bool HasLeadingTrivia => GetLeadingTriviaWidth() != 0;

        public bool HasTrailingTrivia => GetTrailingTriviaWidth() != 0;

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

        public abstract GreenNode WithDiagnostics(ImmutableArray<HapetDiagnostic> diagnostics);

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

        public abstract GreenNode WithAnnotations(ImmutableArray<HapetSyntaxAnnotation> annotations);

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
    }
}
