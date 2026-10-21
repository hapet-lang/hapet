// This file is ported and adapted from the Akbura

namespace HapetFrontend.Syntax.Green
{
    /// <summary>
    /// A SyntaxAnnotation is used to annotate syntax elements with additional information.
    ///
    /// Since syntax elements are immutable, annotating them requires creating new instances of them
    /// with the annotations attached.
    /// </summary>
    public sealed class HapetSyntaxAnnotation : IEquatable<HapetSyntaxAnnotation>
    {
        // use a value identity instead of object identity so a deserialized instance matches the original instance.
        private readonly long _id;
        private static long _nextId;

        // use a value identity instead of object identity so a deserialized instance matches the original instance.
        public short Kind { get; }
        public string Data { get; }

        public HapetSyntaxAnnotation()
        {
            _id = System.Threading.Interlocked.Increment(ref _nextId);
        }

        public HapetSyntaxAnnotation(short kind)
            : this()
        {
            this.Kind = kind;
        }

        public HapetSyntaxAnnotation(short kind, string data)
            : this(kind)
        {
            this.Data = data;
        }

        public bool Equals(HapetSyntaxAnnotation other)
        {
            return other is not null && _id == other._id;
        }

        public static bool operator ==(HapetSyntaxAnnotation left, HapetSyntaxAnnotation right)
        {
            if ((object)left == (object)right)
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(HapetSyntaxAnnotation left, HapetSyntaxAnnotation right)
        {
            if ((object)left == (object)right)
            {
                return false;
            }

            if (left is null || right is null)
            {
                return true;
            }

            return !left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as HapetSyntaxAnnotation);
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }
    }
}
