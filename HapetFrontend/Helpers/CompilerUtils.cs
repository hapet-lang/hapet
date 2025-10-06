using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Types;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace HapetFrontend.Helpers
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class SkipInStackFrameAttribute : Attribute
    { }

    public static class CompilerUtils
    {
        #region Parsing shite helpers
        [DebuggerStepThrough]
        [SkipInStackFrame]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Exception not required. This is for error reporting only.")]
        public static (string function, string file, int line)? GetCallingFunction()
        {
            try
            {
                var trace = new StackTrace(true);
                var frames = trace.GetFrames();

                foreach (var frame in frames)
                {
                    var method = frame.GetMethod();
                    var attribute = method.GetCustomAttributesData().FirstOrDefault(d => d.AttributeType == typeof(SkipInStackFrameAttribute));
                    if (attribute != null)
                        continue;

                    return (method.Name, frame.GetFileName(), frame.GetFileLineNumber());
                }
            }
            catch (Exception)
            { }

            return null;
        }
        #endregion

        public static bool ValidateFilePath(string dir, string filePath, bool isRel, out string path)
        {
            path = filePath;

            if (isRel)
            {
                path = Path.Combine(dir, path);
            }

            path = Path.GetFullPath(path);
            path = path.PathNormalize();

            return true;
        }

        public static string GetNamespace(string projectPath, string rootNamespace, string filePath)
        {
            var projectPathNormalized = Path.GetDirectoryName(projectPath).Replace("\\", "/").TrimEnd('/');
            var filePathNormalized = Path.GetDirectoryName(filePath).Replace("\\", "/").TrimEnd('/');

            StringBuilder uniquePath = new StringBuilder();
            for (int i = 0; i < filePathNormalized.Length; ++i)
            {
                if (i >= projectPathNormalized.Length)
                {
                    uniquePath.Append(filePathNormalized[i]);
                }
            }

            var uniquePathNormalized = uniquePath.ToString().Trim('/').Replace('/', '.');
            // it could be empty if the file is in the same directory as project file
            if (string.IsNullOrWhiteSpace(uniquePathNormalized))
            {
                return rootNamespace;
            }

            return $"{rootNamespace}.{uniquePathNormalized}";
        }

        public static string GetFileRelativePath(string projectPath, string filePath)
        {
            var projectPathNormalized = Path.GetDirectoryName(projectPath).Replace("\\", "/").TrimEnd('/');
            var filePathNormalized = Path.GetDirectoryName(filePath).Replace("\\", "/").TrimEnd('/');

            StringBuilder uniquePath = new StringBuilder();
            for (int i = 0; i < filePathNormalized.Length; ++i)
            {
                if (i >= projectPathNormalized.Length)
                {
                    uniquePath.Append(filePathNormalized[i]);
                }
            }
            var uniquePathNormalized = uniquePath.ToString().Trim('/');
            // it could be empty if the file is in the same directory as project file
            if (string.IsNullOrWhiteSpace(uniquePathNormalized))
            {
                return $"./{Path.GetFileName(filePath)}";
            }

            return $"./{uniquePathNormalized}/{Path.GetFileName(filePath)}";
        }

        /// <summary>
        /// Determines if the paths are equal.
        /// </summary>
        /// <param name="path1">A full path</param>
        /// <param name="path2">Some other full path</param>
        /// <returns>True when they both navigate to the same location.</returns>
        public static bool PathEquals(this string path1, string path2)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            return string.Equals(path1.PathNormalize(), path2.PathNormalize(), comparison);
        }
    }
}
