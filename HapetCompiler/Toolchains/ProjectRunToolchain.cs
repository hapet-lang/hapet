using HapetFrontend;
using HapetFrontend.Entities;
using System.Diagnostics;

namespace HapetCompiler.Toolchains
{
    internal sealed class ProjectRunToolchain
    {
        public CompilerSettings ProjectSettings { get; set; }
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
            CompilerSettings.IsInRunContext = true;

            // just use build 
            ProjectBuildToolchain buildToolchain = new ProjectBuildToolchain(_stopwatch, _cmdArgs);
            var buildResult = buildToolchain.Build(projectPath, messageHandler, false, true);
            // if there was a problem while building the project
            if (buildResult != 0)
                return buildResult;

            StartProcess("C:\\Scripts\\hapet\\test\\SmallTestProject\\bin\\Debug\\SmallTestProject.exe");
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
    }
}
