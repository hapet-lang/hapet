using HapetFrontend.Helpers;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace HapetFrontend.Extensions
{
    public static class StringExtensions
    {
        public static string PathNormalize(this string path)
        {
            return Path.GetFullPath(new Uri(path).LocalPath)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static string GetClassNameWithoutNamespace(this string name)
        {
            var elements = name.Split(".");
            return elements[elements.Length - 1];
        }

        public static string GetNamespaceWithoutClassName(this string name)
        {
            if (!name.Contains('.'))
                return string.Empty;

            var elements = name.Split(".");
            return string.Join('.', elements.SkipLast(1));
        }

        public static string GetPureFuncName(this string name, bool keepExplicitData = true)
        {
            ReadOnlySpan<char> span = name;

            int idxParen = span.IndexOf('(');
            if (idxParen >= 0)
                span = span.Slice(0, idxParen);

            int idxDoubleColon = span.IndexOf("::");
            if (idxDoubleColon >= 0)
                span = span.Slice(idxDoubleColon + 2);

            if (!keepExplicitData)
            {
                int idxLastDot = span.LastIndexOf('.');
                if (idxLastDot >= 0)
                    span = span.Slice(idxLastDot + 1);
            }
            return span.ToString();
        }

        public static string GetClassNameFromFuncName(this string name)
        {
            if (!name.Contains("::"))
                return string.Empty;
            return name.Split("::")[0];
        }

        public static string GetFuncWithClassName(this string name)
        {
            return string.Concat(name.TakeWhile(x => x != '('));
        }
    }
}
