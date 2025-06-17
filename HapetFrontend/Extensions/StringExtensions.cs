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
            ReadOnlySpan<char> span = name;

            int idxDoubleColon = span.IndexOf("::");
            if (idxDoubleColon >= 0)
                return span.Slice(0, idxDoubleColon).ToString();
            return string.Empty;
        }
    }
}
