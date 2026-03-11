using HapetCompiler.Resolvers;
using HapetCompiler.Toolchains.ProjectTemplates;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend.ProjectParser;
using HapetFrontend.Types;
using HapetLastPrepare;
using HapetLsp;
using HapetPostPrepare;
using System.Diagnostics;
using System.Text;

namespace HapetCompiler.Toolchains
{
    internal sealed class ProjectLspToolchain
    {
        public CompilerSettings ProjectSettings { get; set; }
        public ProjectData ProjectData { get; set; }

        private readonly Stopwatch _stopwatch;
        private readonly string[] _cmdArgs; // TODO: use them for ProjectXmlParser
        public ProjectLspToolchain(Stopwatch stopwatch, string[] args)
        {
            _stopwatch = stopwatch;
            _cmdArgs = args;
        }

        async public Task<int> WatchAsync(string projectPath, IMessageHandler messageHandler, IMessageHandler consoleMessageHandler)
        {
            // check for --help
            if (projectPath == "--help" || projectPath == "-h")
            {
                // print help
                PrintHelp(consoleMessageHandler);
                return 0;
            }

            // save type context
            var cachedTypeContext = HapetType.CurrentTypeContext;

            messageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_stopwatch.Elapsed)} Initializing LSP..."], null, ReportType.Info);

            // setting the type context
            HapetType.CurrentTypeContext = new TypeContext();
            // creating settings instances for the project
            ProjectSettings = new CompilerSettings();
            // setting that hapet is running as LSP server
            ProjectSettings.IsLspCompilation = true;
            ProjectData = new ProjectData();
            // parsing project .hptproj file
            var projectParser = new ProjectXmlParser(projectPath, ProjectSettings, ProjectData, messageHandler);
            projectParser.PrepareProjectFile(); // setting compiler settings from project

            // setting pointer size for the whole assembly
            HapetType.CurrentTypeContext.PointerSize = ProjectSettings.TargetPlatformData.PointerSize;
            HapetType.CurrentTypeContext.Init();
            // creating the compiler and preparers
            var compiler = new Compiler(ProjectSettings, ProjectData, messageHandler);
            var postPreparer = new PostPrepare(compiler);
            var lastPreparer = new LastPrepare(compiler, postPreparer);
            compiler.InitGlobalScope();
            compiler.CompilationStopwatch = _stopwatch;

            // references
            ProjectReferencesResolver resolver = new ProjectReferencesResolver();
            resolver.ResolveProjectShite(ProjectData, ProjectSettings, compiler, projectParser);

            // gen ast shite
            compiler.GenerateAstTree();
            // post prepare without meta file
            int _ = postPreparer.StartPreparation(false, forLsp: true);
            // full last prepare is not required for LSP
            int __ = lastPreparer.StartPreparation(true);

            messageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(compiler.CompilationStopwatch.Elapsed)} Starting LSP..."], null, ReportType.Info);

            // starting server
            LspServer server = new LspServer();
            await server.StartAsync(projectParser, compiler, postPreparer, lastPreparer, MakeResolveAgain, _cmdArgs.Contains("--tcp"));

            // restore it
            HapetType.CurrentTypeContext = cachedTypeContext;
            return (int)CompilerErrors.Ok;

            void MakeResolveAgain()
            {
                projectParser.ParseFile(projectParser.ProjectFileText);
                projectParser.PrepareFile();
                projectParser.PrepareProjectFile();

                // setting pointer size for the whole assembly
                HapetType.CurrentTypeContext.PointerSize = ProjectSettings.TargetPlatformData.PointerSize;
                HapetType.CurrentTypeContext.Init();

                resolver.ResolveProjectShite(ProjectData, ProjectSettings, compiler, projectParser);
            }
        }

        private void PrintHelp(IMessageHandler messageHandler)
        {
            messageHandler.ReportMessage([$"Usage: \n  hapet lsp <project> <args> \n"], null, ReportType.Info);
            messageHandler.ReportMessage([$"Parameters: "], null, ReportType.Info);
            messageHandler.ReportMessage([$"  --tcp \t\t With this parameter LSP server starts over TCP on 5007 port."], null, ReportType.Info);
            messageHandler.ReportMessage([$"\t\t\t By default it starts over stdin/stdout."], null, ReportType.Info);
        }
    }
}
