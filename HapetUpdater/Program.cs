using HapetCommon;
using Microsoft.VisualBasic;
using System.Diagnostics;

namespace HapetUpdater
{
    internal class Program
    {
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
            const string logFile = "updater_log.txt";
            File.WriteAllText(logFile, "");

            var updaterDir = AppContext.BaseDirectory;
            var tempPath = Path.Combine(updaterDir, "..", CompilerUtils.HAPET_TEMP_UPDATE_FOLDER);
            var tmpExists = Directory.Exists(tempPath);
            File.AppendAllText(logFile, $"Updater dir: {updaterDir}\n Tmp dir: {tempPath}\n Exists tmp: {tmpExists}");


        }
    }
}
