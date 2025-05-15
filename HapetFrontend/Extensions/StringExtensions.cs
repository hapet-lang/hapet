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
            string rightPart;
            if (!name.Contains("::"))
                rightPart = string.Concat(name.TakeWhile(x => x != '('));
            else
                rightPart = string.Concat(name.Split("::")[1].TakeWhile(x => x != '('));

            if (!rightPart.Contains('.'))
                return rightPart;

            var splitted = rightPart.Split('.');
            var result = splitted[^1];
            if (keepExplicitData)
            {
                // this is done to handle shite like:
                // bool System.Collections.IStructuralEquatable.Equals(..
                // and make it to this:
                // bool IStructuralEquatable.Equals(..
                result = string.Join('.', splitted[^2], splitted[^1]);
            }
            return result;
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
