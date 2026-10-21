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

        /// <summary>
        /// Search a sorted integer array for the target value in O(log N) time.
        /// </summary>
        /// <param name="array">
        /// The array of integers which must be sorted in ascending order.
        /// </param>
        /// <param name="value">
        /// The target value.
        /// </param>
        /// <returns>An index in the array pointing to the position where <paramref name="value"/> should be
        /// inserted in order to maintain the sorted order. All values to the right of this position will be
        /// strictly greater than <paramref name="value"/>. Note that this may return a position off the end
        /// of the array if all elements are less than or equal to <paramref name="value"/>.</returns>
        public static int BinarySearchUpperBound(this int[] array, int value)
        {
            var low = 0;
            var high = array.Length - 1;

            while (low <= high)
            {
                var middle = low + (high - low >> 1);
                if (array[middle] > value)
                {
                    high = middle - 1;
                }
                else
                {
                    low = middle + 1;
                }
            }

            return low;
        }
    }
}
