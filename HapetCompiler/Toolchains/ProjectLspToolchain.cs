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

            // setting that hapet is running as LSP server
            CompilerSettings.IsInLspContext = true;

            // save type context
            var cachedTypeContext = HapetType.CurrentTypeContext;

            // setting the type context
            HapetType.CurrentTypeContext = new TypeContext();
            // creating settings instances for the project
            ProjectData = new ProjectData();
            // parsing project .hptproj file
            var projectParser = new ProjectXmlParser(projectPath, ProjectData, messageHandler);
            projectParser.PrepareProjectFile(); // setting compiler settings from project

            // setting pointer size for the whole assembly
            HapetType.CurrentTypeContext.PointerSize = CompilerSettings.TargetPlatformData.PointerSize;
            HapetType.CurrentTypeContext.Init();
            // creating the compiler and preparers
            var compiler = new Compiler(ProjectData, messageHandler);
            var postPreparer = new PostPrepare(compiler);
            var lastPreparer = new LastPrepare(compiler, postPreparer);
            compiler.InitGlobalScope();
            compiler.CompilationStopwatch = _stopwatch;

            // references
            ProjectReferencesResolver resolver = new ProjectReferencesResolver();
            resolver.ResolveProjectShite(ProjectData, compiler, projectParser);

            // gen ast shite
            compiler.GenerateAstTree();
            // post prepare without meta file
            int _ = postPreparer.StartPreparation(false, forLsp: true);
            // full last prepare is not required for LSP
            int __ = lastPreparer.StartPreparation(true);

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
                HapetType.CurrentTypeContext.PointerSize = CompilerSettings.TargetPlatformData.PointerSize;
                HapetType.CurrentTypeContext.Init();

                resolver.ResolveProjectShite(ProjectData, compiler, projectParser);
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
