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
            // we are getting the generic part to append it at the end
            int indexofGenericEntry = name.IndexOf(GenericsHelper.GENERIC_BEGIN);
            string toAppend = string.Empty;
            if (indexofGenericEntry != -1)
            {
                toAppend = name.Substring(indexofGenericEntry, name.Length - indexofGenericEntry);
                name = name.Substring(0, indexofGenericEntry);
            }

            var elements = name.Split(".");
            return elements[elements.Length - 1] + toAppend;
        }

        public static string GetNamespaceWithoutClassName(this string name)
        {
            // we are getting the generic part to append it at the end
            int indexofGenericEntry = name.IndexOf(GenericsHelper.GENERIC_BEGIN);
            if (indexofGenericEntry != -1)
            {
                name = name.Substring(0, indexofGenericEntry);
            }

            if (!name.Contains('.'))
                return string.Empty;

            var elements = name.Split(".");
            return string.Join('.', elements.SkipLast(1));
        }

        public static string GetPureFuncName(this string name)
        {
            if (!name.Contains("::"))
                return string.Concat(name.TakeWhile(x => x != '('));
            return string.Concat(name.Split("::")[1].TakeWhile(x => x != '('));
        }

        public static string GetClassNameFromFuncName(this string name)
        {
            if (!name.Contains("::"))
                return string.Empty;
            return name.Split("::")[0];
        }

        public static int GetGenericsAmount(this string name)
        {
            int amount = 0;

            // getting the index of the first entry
            int genIndex = name.IndexOf(GenericsHelper.GENERIC_BEGIN);
            if (genIndex == -1)
                return 0; // there are no generics

            // at least one generic exists if the _GB_ exists
            amount++;

            // if > 0 then there was a _GB_ and no _GE_ yet. if < 0 - probably error :)
            int currentState = 0;

            string currentSearchString = name[(genIndex + GenericsHelper.GENERIC_BEGIN.Length)..];
            while (currentSearchString.Length > 0)
            {
                if (currentState == 0 && currentSearchString.StartsWith(GenericsHelper.GENERIC_DELIM))
                {
                    amount++;
                    currentSearchString = currentSearchString[GenericsHelper.GENERIC_DELIM.Length..];
                    continue;
                }
                else if (currentState == 0 && currentSearchString.StartsWith(GenericsHelper.GENERIC_END))
                {
                    // all found
                    break;
                }
                else if (currentSearchString.StartsWith(GenericsHelper.GENERIC_BEGIN))
                {
                    currentState++;
                }
                else if (currentSearchString.StartsWith(GenericsHelper.GENERIC_END))
                {
                    currentState--;
                }
                currentSearchString = currentSearchString[1..];
            }
            return amount;
        }
    }
}
