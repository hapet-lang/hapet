namespace HapetFrontend.Other
{
    /// <summary>
    /// Helper class for hash codes. Taken from Akbura https://github.com/Asaicraft/Akbura
    /// </summary>
    internal static class HashCode
    {
        /// <summary>
        /// The offset bias value used in the FNV-1a algorithm
        /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
        /// </summary>
        public const int FnvOffsetBias = unchecked((int)2166136261);

        /// <summary>
        /// The generative factor used in the FNV-1a algorithm
        /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
        /// </summary>
        public const int FnvPrime = 16777619;

        /// <summary>
        /// Compute the hashcode of a string using FNV-1a
        /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
        /// </summary>
        /// <param name="text">The input string</param>
        /// <returns>The FNV-1a hash code of <paramref name="text"/></returns>
        internal static int GetFNVHashCode(System.Text.StringBuilder text)
        {
            var hashCode = FnvOffsetBias;

#if NETCOREAPP3_1_OR_GREATER
            foreach (var chunk in text.GetChunks())
            {
                hashCode = CombineFNVHash(hashCode, chunk.Span);
            }
#else
        // StringBuilder.GetChunks is not available in this target framework. Since there is no other direct access
        // to the underlying storage spans of StringBuilder, we fall back to using slower per-character operations.
        var end = text.Length;

        for (var i = 0; i < end; i++)
        {
            hashCode = unchecked((hashCode ^ text[i]) * FnvPrime);
        }
#endif

            return hashCode;
        }

        public static int GetFNVHashCode(ReadOnlySpan<char> data)
        {
            var hashCode = HapetFrontend.Other.HashCode.FnvOffsetBias;

            for (var i = 0; i < data.Length; i++)
            {
                hashCode = unchecked((hashCode ^ data[i]) * HapetFrontend.Other.HashCode.FnvPrime);
            }

            return hashCode;
        }

        internal static int CombineFNVHash(int hashCode, string text)
            => CombineFNVHash(hashCode, text.AsSpan());

        /// <summary>
        /// Combine a char with an existing FNV-1a hash code
        /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
        /// </summary>
        /// <param name="hashCode">The accumulated hash code</param>
        /// <param name="ch">The new character to combine</param>
        /// <returns>The result of combining <paramref name="hashCode"/> with <paramref name="ch"/> using the FNV-1a algorithm</returns>
        internal static int CombineFNVHash(int hashCode, char ch)
        {
            return unchecked((hashCode ^ ch) * FnvPrime);
        }

        internal static int CombineFNVHash(int hashCode, ReadOnlySpan<char> data)
        {
            for (var i = 0; i < data.Length; i++)
            {
                hashCode = unchecked((hashCode ^ data[i]) * FnvPrime);
            }

            return hashCode;
        }
    }
}
