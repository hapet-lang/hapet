using Microsoft.VisualBasic;
using System.Diagnostics;

namespace HapetUpdater
{
    internal class Program
    {
        const string _logFile = "updater_log.txt";

        const string HAPET_TEMP_UPDATE_FOLDER = "hapet_temp_update";
        const string UPDATER_FILE_NAME = "hapet-replacer";

        static void Main(string[] args)
        {
            if (args.Length > 0 && int.TryParse(args[0], out int parentPid))
            {
                try
                {
                    var parentProcess = Process.GetProcessById(parentPid);
                    parentProcess.WaitForExit(5000);
                    Process[] pname = Process.GetProcessesByName("hapet");
                    if (pname.Length != 0)
                        pname.FirstOrDefault()?.WaitForExit(5000);
                }
                catch { }
            }
            // clear log file
            File.WriteAllText(_logFile, "");

            // check for hapet running instances
            Process[] hapetProcesses = Process.GetProcessesByName("hapet");
            if (hapetProcesses.Length != 0)
                File.AppendAllText(_logFile, $"There is multiple hapet instances running! Update may fail!\n");
            else
                File.AppendAllText(_logFile, $"No hapet instances running. Ok.\n");

            var hapetDir = AppContext.BaseDirectory;
            var tempPath = Path.Combine(hapetDir, HAPET_TEMP_UPDATE_FOLDER);
            var tmpExists = Directory.Exists(tempPath);
            File.AppendAllText(_logFile, $"Updater dir: {hapetDir}\nTmp dir: {tempPath}\nExists tmp: {tmpExists}\n");

            // if tmp does not exist - nothing to update
            if (!tmpExists)
            {
                File.AppendAllText(_logFile, $"Closing updater because there is no tmp dir.\n");
                return;
            }

            CopyHapetFiles(tempPath, hapetDir);
            File.AppendAllText(_logFile, $"Probably done.\n");
        }

        private static void CopyHapetFiles(string source, string destination)
        {
            File.AppendAllText(_logFile, $"Dest: {destination}\n");

            if (!Directory.Exists(source))
                return;

            source = source.TrimEnd('/').TrimEnd('\\');

            var items = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            int count = items.Length;
            for (int i = 0; i < count; ++i)
            {
                var item = items[i];
                string relat = Path.GetRelativePath(source, item).Trim('/').Trim('\\');
                // skip itself
                if (relat.Contains(UPDATER_FILE_NAME))
                    continue;

                File.AppendAllText(_logFile, $"Real path: {item}, Relative: {relat}\n");

                string dst = $"{destination}/{relat}";
                try
                {
                    string dr = Path.GetDirectoryName(dst);
                    if (!Directory.Exists(dr))
                        Directory.CreateDirectory(dr);
                    File.Copy(item, dst, true);
                }
                catch (Exception e)
                {
                    File.AppendAllText(_logFile, $"Error while swapping the file: {item}: \n{e} \n");
                }
            }
        }
    }
}
