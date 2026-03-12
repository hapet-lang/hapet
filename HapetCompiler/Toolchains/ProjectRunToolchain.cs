using HapetCompiler.Toolchains.ProjectTemplates;
using HapetFrontend;
using HapetFrontend.Entities;
using System.Diagnostics;
using System.Text;

namespace HapetCompiler.Toolchains
{
    internal sealed class ProjectRunToolchain
    {
        public ProjectData ProjectData { get; set; }

        private readonly Stopwatch _stopwatch;
        private readonly string[] _cmdArgs; // TODO: use them for ProjectXmlParser
        public ProjectRunToolchain(Stopwatch stopwatch, string[] args)
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
            ProjectBuildToolchain buildToolchain = new ProjectBuildToolchain(_stopwatch, _cmdArgs);
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
            messageHandler.ReportMessage([$"Usage: \n  hapet run <project> <args> \n"], null, ReportType.Info);
        }
    }
}
