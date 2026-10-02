
using HapetFrontend.Syntax.Red;

namespace HapetFrontend.Syntax.Green
{
    public abstract class GreenNode
    {
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
        private readonly ushort _rawKind;

        public int FullWidth
        {
            get => _fullWidth;
            protected init => _fullWidth = value;
        }

        public SyntaxKind Kind => (SyntaxKind)_rawKind;

        public int SmallSlotCount => _nodeFlagsAndSlotCount & 0b_0000_0000_0000_1111;

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

        protected GreenNode(ushort kind)
        {
            _rawKind = kind;
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
    }
}
