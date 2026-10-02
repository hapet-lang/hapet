using HapetFrontend.Entities;
using Microsoft.Language.Xml;
using System.Text;

namespace HapetFrontend.Lexer
{
    internal static class QuickQuickSetExtensions
    {
        public static string Add(this QuickQuickSet<string> obj, ReadOnlySpan<char> val)
        {
            var hash = HapetFrontend.Other.HashCode.GetFNVHashCode(val);
            var output = val.ToString();
            obj.AddOrReplace(hash, output);
            return output;
        }

        public static bool TryGet(this QuickQuickSet<string> obj, ReadOnlySpan<char> key, out string value)
        {
            var hash = HapetFrontend.Other.HashCode.GetFNVHashCode(key);
            return obj.TryGet(hash, out value);
        }

        public static string Intern(this QuickQuickSet<string> obj, ReadOnlySpan<char> key)
        {
            var hash = HapetFrontend.Other.HashCode.GetFNVHashCode(key);
            if (obj.TryGet(hash, out string value))
                return value;

            var output = key.ToString();
            obj.AddOrReplace(hash, output);
            return output;
        }

        public static string Intern(this QuickQuickSet<string> obj, StringBuilder key)
        {
            var hash = HapetFrontend.Other.HashCode.GetFNVHashCode(key);
            if (obj.TryGet(hash, out string value))
                return value;

            var output = key.ToString();
            obj.AddOrReplace(hash, output);
            return output;
        }
    }
}
