using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Helpers;
using System.Xml.Linq;

namespace HapetFrontend.Types
{
    public abstract class HapetType
    {
        /// <summary>
        /// The name of the type alias like 'int', 'bool' and etc.
        /// </summary>
        public abstract string TypeName { get; }

        /// <summary>
        /// Current type context is used to handle context dependent shite.
        /// For example PointerSize could be different for different projects.
        /// Or String/Array types WOULD be different for different projects!
        /// </summary>
        public static TypeContext CurrentTypeContext { get; set; }

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

        public virtual int GetSize() => _size;
        public virtual int GetAlignment() => _alignment;

        public abstract AstExpression GetAst();

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

        public virtual int Match(HapetType concrete)
        {
            if (concrete is ReferenceType r)
                concrete = r.TargetType;

            if (this == concrete)
                return 0;
            return -1;
        }

        public static string AsString(HapetType type, bool prettifyType = false)
        {
            string typeString = type == null ? "[Undefined]" : type.ToString();

            // prettify generics and other shite
            if (prettifyType)
            {
                // if it is a ptr for a class
                if (type is PointerType ptr && ptr.TargetType is ClassType cls)
                {
                    // if it is an impl of a generic containing type
                    if (cls.Declaration.IsImplOfGeneric)
                    {
                        return GenericsHelper.GetPrettyGenericImplName(typeString);
                    }
                    // if it is a generic type
                    else if (cls.Declaration.IsGenericType)
                    {
                        return GenericsHelper.GetPrettyGenericTypeName(typeString);
                    }
                }
                
            }

            return typeString;
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

        // used for classes and structs
        public bool IsInheritedFrom(ClassType type, bool checkParents = true)
        {
            if (type == null)
                return false;

            List<AstNestedExpr> inhFrom;
            if (this is ClassType clsT)
                inhFrom = clsT.Declaration.InheritedFrom;
            else if (this is StructType strT)
                inhFrom = strT.Declaration.InheritedFrom;
            else
                return false;

            foreach (var expr in inhFrom)
            {
                var outT = expr.OutType as ClassType;
                if (outT == type || (checkParents && outT.IsInheritedFrom(type)))
                    return true;
            }
            return false;
        }
    }
}
