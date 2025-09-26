#define PRINT_INTERMEDIATE_TIME

using HapetBackend.Llvm;
using HapetFrontend.Entities;
using HapetFrontend;
using System.Diagnostics;
using HapetFrontend.Helpers;
using HapetCompiler.Resolvers;
using HapetPostPrepare;
using HapetFrontend.Types;
using HapetLastPrepare;
using HapetFrontend.ProjectParser;

namespace HapetCompiler.Toolchains
{
    internal sealed class ProjectBuildToolchain
    {
        public CompilerSettings ProjectSettings { get; set; }
        public ProjectData ProjectData { get; set; }

        private readonly Stopwatch _stopwatch;
        private readonly string[] _cmdArgs; // TODO: use them for ProjectXmlParser
        public ProjectBuildToolchain(Stopwatch stopwatch, string[] args)
        {
            _stopwatch = stopwatch;
            _cmdArgs = args;
        }

        public int Build(string projectPath, IMessageHandler messageHandler, bool referenced = false, bool makeCodegen = true)
        {
            // save type context
            var cachedTypeContext = HapetType.CurrentTypeContext;

            if (!referenced)
                messageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_stopwatch.Elapsed)} Project preparation..."], null, ReportType.Info);
            // setting the type context
            HapetType.CurrentTypeContext = new TypeContext();
            // creating settings instances for the project
            ProjectSettings = new CompilerSettings();
            // saving that the build is referenced or not
            ProjectSettings.IsReferencedCompilation = referenced;
            ProjectData = new ProjectData();
            // parsing project .hptproj file
            var projectParser = new ProjectXmlParser(projectPath, ProjectSettings, ProjectData, messageHandler);
            projectParser.PrepareProjectFile(); // setting compiler settings from project
            if (messageHandler.HasErrors)
            {
                OnExit();
                return (int)CompilerErrors.ProjectFileParseError; // proj file parsing errors
            }

#if DEBUG && PRINT_INTERMEDIATE_TIME
            if (!referenced)
                messageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_stopwatch.Elapsed)} After project file parsing..."], null, ReportType.Info);
#endif

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

            if (!referenced)
                messageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_stopwatch.Elapsed)} Parsing..."], null, ReportType.Info);
            // gen ast shite
            compiler.GenerateAstTree();
            if (messageHandler.HasErrors)
            {
                OnExit();
                return (int)CompilerErrors.ParsingError; // parsing errors
            }

            if (!referenced)
                messageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_stopwatch.Elapsed)} Post preparation..."], null, ReportType.Info);
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

            if (!referenced)
                messageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_stopwatch.Elapsed)} Last preparation..."], null, ReportType.Info);
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

            // if codegen required
            if (makeCodegen)
            {
                if (!referenced)
                    messageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_stopwatch.Elapsed)} Code generation..."], null, ReportType.Info);
                // code gen
                bool codeGenOk = GenerateAndCompileCode(compiler, postPreparer, resolver, messageHandler);
                if (messageHandler.HasErrors || !codeGenOk)
                {
                    OnExit();
                    return (int)CompilerErrors.CodeGenerationError; // code generation errors
                }
            }

            // all is ok :)
            if (!referenced)
                messageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_stopwatch.Elapsed)} Done..."], null, ReportType.Info);

            OnExit();
            return (int)CompilerErrors.Ok;

            void OnExit()
            {
                // restore it
                HapetType.CurrentTypeContext = cachedTypeContext;
            }
        }

        private static bool GenerateAndCompileCode(Compiler compiler, PostPrepare postPreparer, ProjectReferencesResolver resolver, IMessageHandler messageHandler)
        {
            var generator = new LlvmCodeGenerator();
            bool success = generator.GenerateCode(compiler, postPreparer, messageHandler);
            if (!success)
                return false;

            return generator.CompileCode(resolver.PathsToLinkWith, resolver.LibrariesToLinkWith, messageHandler);
        }
    }
}
