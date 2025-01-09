namespace HapetFrontend.Types
{
    public abstract class HapetType
    {
        /// <summary>
        /// Like "class Pivo<T>" where Pivo is a ClassType and IsGenericType - true (TODO: not sure)
        /// </summary>
        public virtual bool IsGenericType { get; } = false;

        /// <summary>
        /// The name of the type alias like 'int', 'bool' and etc.
        /// </summary>
        public abstract string TypeName { get; }

        /// <summary>
        /// The size of a pointer on the currently selected platform
        /// </summary>
        public static int PointerSize => Compiler.AssemblyPointerSize;

        protected int _size = -1;
        protected int _alignment = -1;

        protected HapetType(int size, int align)
        {
            if (size < 0)
                throw new ArgumentOutOfRangeException(nameof(size));
            if (align < 0)
                throw new ArgumentOutOfRangeException(nameof(align));
            _size = size;
            _alignment = align;
        }

        protected HapetType()
        {
        }

        public int GetSize() => _size;
        public int GetAlignment() => _alignment;

        public void SetSizeAndAlignment(int size, int align)
        {
            _size = size;
            _alignment = align;
        }

        public static bool operator ==(HapetType a, HapetType b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            return a.Equals(b);
        }

        public static bool operator !=(HapetType a, HapetType b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public virtual int Match(HapetType concrete)
        {
            if (concrete is ReferenceType r)
                concrete = r.TargetType;

            if (this == concrete)
                return 0;
            return -1;
        }

        /// <summary>
        /// Returns the preferred type of two other types
        /// Usually used for Numeric types and for Binary expressions
        /// Like: bool a = (5 == 6.3); Where preferred type of 5 would be a FloatType
        /// </summary>
        /// <param name="first">The first type</param>
        /// <param name="second">The second type</param>
        /// <param name="tookFirst">Returns 'true' if type of the first param was used. Otherwise 'false' (the second was used)</param>
        /// <returns>Preferred type</returns>
        public static HapetType GetPreferredTypeOf(HapetType first, HapetType second, out bool tookFirst)
        {
            HapetType outType;
            if ((first is FloatType && second is IntType) || (first is FloatType && second is CharType))
            {
                outType = first;
                tookFirst = true;
            }
            else if ((second is FloatType && first is IntType) || (second is FloatType && first is CharType))
            {
                outType = second;
                tookFirst = false;
            }
            else if (second is IntType sInt && first is IntType fInt)
            {
                if (sInt.Signed && !fInt.Signed)
                {
                    outType = sInt;
                    tookFirst = false;
                }
                else if (!sInt.Signed && fInt.Signed)
                {
                    outType = fInt;
                    tookFirst = true;
                }
                else
                {
                    tookFirst = sInt.GetSize() < fInt.GetSize();
                    outType = tookFirst ? fInt : sInt;
                }
            }
            else
            {
                tookFirst = second.GetSize() < first.GetSize();
                outType = tookFirst ? first : second;
            }
            return outType;
        }
    }
}
