using HapetCompiler.Toolchains.ProjectTemplates;
using HapetFrontend;
using HapetFrontend.Entities;
using System.Diagnostics;
using System.Text;

namespace HapetCompiler.Toolchains
{
    internal sealed class HapetRunToolchain
    {
        public ProjectData ProjectData { get; set; }

        private readonly Stopwatch _stopwatch;
        private readonly string[] _cmdArgs; // TODO: use them for ProjectXmlParser
        public HapetRunToolchain(Stopwatch stopwatch, string[] args)
        {
            _stopwatch = stopwatch;
            _cmdArgs = args;
        }

        public int Run(string projectPath, IMessageHandler messageHandler)
        {
            // check for --help
            if (projectPath == "--help" || projectPath == "-h")
            {
                // print help
                PrintHelp(messageHandler);
                return 0;
            }

            CompilerSettings.IsInRunContext = true;

            // just use build 
            HapetBuildToolchain buildToolchain = new HapetBuildToolchain(_stopwatch, _cmdArgs);
            var buildResult = buildToolchain.Build(projectPath, messageHandler, out string outFilePath, false, true);
            // if there was a problem while building the project
            if (buildResult != 0)
                return buildResult;

            StartProcess(outFilePath);
            return 0;
        }

        private void StartProcess(string executablePath)
        {
            Process executableProcess = new Process();
            executableProcess.StartInfo.FileName = executablePath;
            executableProcess.StartInfo.UseShellExecute = false;
            executableProcess.Start();

            // make it run sync
            executableProcess.WaitForExit();
        }

        private void PrintHelp(IMessageHandler messageHandler)
        {
            messageHandler.ReportMessage($"Usage: \n  hapet run <project> <args> \n");
            messageHandler.ReportMessage($"Parameters: ");
            messageHandler.ReportMessage($"  -t|--target <PLATFORM> \t The platform for which the project will be built.");
            messageHandler.ReportMessage($"\t\t\t\t If the target platform is not specified, the project will be built for the current platform.");
            messageHandler.ReportMessage($"\t\t\t\t List of available platforms: win-x64, win-x86, linux-x64, linux-x86.");
            messageHandler.ReportMessage($"  --verbose \t\t\t Detailed output while building.");
            messageHandler.ReportMessage($"  --debug|--release \t\t Project build configuration.");
            messageHandler.ReportMessage($"\t\t\t\t If no configuration is specified, --debug is used.");
        }
    }
}
