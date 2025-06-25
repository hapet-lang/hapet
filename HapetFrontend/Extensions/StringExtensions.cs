using HapetFrontend.Ast.Expressions;

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
    }
}
