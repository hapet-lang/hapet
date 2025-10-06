using HapetFrontend.Ast.Expressions;
using System.Text.RegularExpressions;

namespace HapetFrontend.Extensions
{
    public static class StringExtensions
    {
        public static IEnumerable<string> Scan(this string value, string pattern)
        {
            var regex = new Regex(pattern);
            var matches = regex.Match(value);

            foreach (Group c in matches.Groups)
            {
                yield return c.Value;
            }
        }

        public static IEnumerable<string> Scan1(this string value, string pattern)
        {
            return value.Scan(pattern).Skip(1);
        }

        public static string PathNormalize(this string path)
        {
            return Path.GetFullPath(path)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static string GetClassNameWithoutNamespace(this string name)
        {
            ReadOnlySpan<char> span = name;

            int idxLastDot = span.LastIndexOf('.');
            if (idxLastDot >= 0) 
            {
                span = span.Slice(idxLastDot + 1);
                return span.ToString();
            }
            return name;
        }

        public static string GetNamespaceWithoutClassName(this string name)
        {
            ReadOnlySpan<char> span = name;

            int idxLastDot = span.LastIndexOf('.');
            if (idxLastDot >= 0)
                return span.Slice(0, idxLastDot).ToString();
            return string.Empty;
        }
    }
}
