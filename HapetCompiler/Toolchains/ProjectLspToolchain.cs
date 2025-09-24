using HapetCompiler.ProjectConf;
using HapetCompiler.Resolvers;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend.Types;
using HapetLastPrepare;
using HapetPostPrepare;
using System.Diagnostics;

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

        public int Watch(string projectPath, IMessageHandler messageHandler)
        {
            // save type context
            var cachedTypeContext = HapetType.CurrentTypeContext;

            // setting the type context
            HapetType.CurrentTypeContext = new TypeContext();
            // creating settings instances for the project
            ProjectSettings = new CompilerSettings();
            ProjectData = new ProjectData();
            // parsing project .hptproj file
            var projectParser = new ProjectXmlParser(projectPath, ProjectSettings, ProjectData, messageHandler);
            projectParser.PrepareProjectFile(); // setting compiler settings from project
            if (messageHandler.HasErrors)
            {
                OnExit();
                return (int)CompilerErrors.ProjectFileParseError; // proj file parsing errors
            }

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
            resolver.ResolveProjectShite(ProjectData, ProjectSettings, compiler);
            if (messageHandler.HasErrors)
            {
                OnExit();
                return (int)CompilerErrors.ProjectReferencesError; // references errors
            }

            // gen ast shite
            compiler.GenerateAstTree();
            if (messageHandler.HasErrors)
            {
                OnExit();
                return (int)CompilerErrors.ParsingError; // parsing errors
            }

            // post prepare
            int ppResult = postPreparer.StartPreparation();
            if (ppResult != 0)
            {
                OnExit();
                return ppResult; // post prepare errors
            }
            if (messageHandler.HasErrors)
            {
                OnExit();
                return (int)CompilerErrors.PostPrepareError; // post prepare errors
            }

            // last prepare
            int lpResult = lastPreparer.StartPreparation();
            if (lpResult != 0)
            {
                OnExit();
                return lpResult; // last prepare errors
            }
            if (messageHandler.HasErrors)
            {
                OnExit();
                return (int)CompilerErrors.LastPrepareError; // last prepare errors
            }

            OnExit();
            return (int)CompilerErrors.Ok;

            void OnExit()
            {
                // restore it
                HapetType.CurrentTypeContext = cachedTypeContext;
            }
        }
    }
}
