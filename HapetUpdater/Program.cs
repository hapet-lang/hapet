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
                    parentProcess.WaitForExit();
                }
                catch { }
            }

            Console.WriteLine("Replacing files...");
            Console.WriteLine("Done...");
        }
    }
}
