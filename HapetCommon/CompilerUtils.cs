using HapetCommon.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HapetCommon
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

        public static string CurrentHapetDirectory => AppContext.BaseDirectory;

        public const string HAPET_DOWNLOAD_LINK = "https://hapetlang.com/resources/hapet";
        public const string COMPUTED_HASH_FILENAME = "computed_hash.json";
        public const string TMP_COMPUTED_HASH_FILENAME = "tmp_computed_hash.json";
        public const string HAPET_TEMP_UPDATE_FOLDER = "hapet_temp_update";
        public const string UPDATER_FILE_NAME = "HapetUpdater";

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

        public static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            // Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            // Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        public static void DeleteEverythingUnderDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    return;

                DirectoryInfo directory = new DirectoryInfo(path);

                foreach (FileInfo file in directory.GetFiles())
                {
                    file.Delete();
                }

                foreach (DirectoryInfo dir in directory.GetDirectories())
                {
                    dir.Delete(true);
                }

                directory.Delete(true);
            }
            catch
            {
                // LoggingService.Error("Error while cleaning directory", ex);
            }
        }


        // ------------------------- FUNCAD ------------------------------

        /// <summary>
        /// Returns 'true' is the provided number is a power of two (including 0)
        /// </summary>
        /// <param name="x">The number to be checked</param>
        /// <returns>Is it power of two</returns>
        public static bool IsPowerOfTwo(int x)
        {
            return (x & (x - 1)) == 0;
        }

        /// <summary>
        /// Prettifies <see cref="TimeSpan"/> into 'MM:SS:MS'
        /// </summary>
        /// <param name="ts">The TimeSpan</param>
        /// <returns>Prettified string</returns>
        public static string GetPrettyTimeString(TimeSpan ts)
        {
            ulong totalMs = (ulong)ts.TotalMilliseconds;

            string showMs = (totalMs % 1000).ToString("D3");
            string showS = ((totalMs / 1000) % 60).ToString("D2");
            string showM = ((totalMs / 1000 / 60) % 60).ToString("D2");

            return $"{showM}:{showS}:{showMs}";
        }

        public static string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                return Convert.ToHexString(hashBytes);
            }
        }

        #region Process shite
        public static Process StartProcess(string exe, List<string> argList = null, string workingDirectory = null, DataReceivedEventHandler stdout = null, DataReceivedEventHandler stderr = null)
        {
            argList = argList ?? new List<string>();
            var args = string.Join(" ", argList.Select(a =>
            {
                if (a.Contains(" ", StringComparison.InvariantCulture))
                    return $"\"{a}\"";
                return a;
            }));
            return StartProcess(exe, args, workingDirectory, stdout, stderr);
        }

        public static Process StartProcess(string exe, string args = null, string workingDirectory = null, DataReceivedEventHandler stdout = null, DataReceivedEventHandler stderr = null, bool useShellExecute = false, bool createNoWindow = true)
        {
            // Console.WriteLine($"{exe} {args}");

            var process = new Process();
            process.StartInfo.FileName = exe;
            if (workingDirectory != null)
                process.StartInfo.WorkingDirectory = workingDirectory;
            if (args != null)
                process.StartInfo.Arguments = args;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            process.StartInfo.UseShellExecute = useShellExecute;
            process.StartInfo.CreateNoWindow = createNoWindow;

            // setting output lang to eng
            process.StartInfo.EnvironmentVariables["VSLANG"] = "1033";

            if (stdout != null)
            {
                process.StartInfo.RedirectStandardOutput = true;
                process.OutputDataReceived += stdout;
            }

            if (stderr != null)
            {
                process.StartInfo.RedirectStandardError = true;
                process.ErrorDataReceived += stderr;
            }

            process.Start();

            if (stdout != null)
                process.BeginOutputReadLine();
            if (stderr != null)
                process.BeginErrorReadLine();

            return process;
        }
        #endregion
    }
}
