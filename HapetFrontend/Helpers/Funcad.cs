using System.Diagnostics;

namespace HapetFrontend.Helpers
{
    public static class Funcad
    {
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

		public static void CopyFilesRecursively(string sourcePath, string targetPath)
		{
			//Now Create all of the directories
			foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
			{
				Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
			}

			//Copy all the files & Replaces any files with the same name
			foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
			{
				File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
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
