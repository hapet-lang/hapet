using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace HapetFrontend.Extensions
{
    public static class ArrayExtensions
    {
        public static ImmutableArray<T> ToImmutableArrayUnsafe<T>(this T[] array)
        {
            return Unsafe.As<T[], ImmutableArray<T>>(ref array);
        }

        public static T[] ToArrayUnsafe<T>(this ImmutableArray<T> array)
        {
            return Unsafe.As<ImmutableArray<T>, T[]>(ref array);
        }
    }
}
