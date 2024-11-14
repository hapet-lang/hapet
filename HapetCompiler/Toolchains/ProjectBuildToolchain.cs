using HapetBackend.Llvm;
using HapetFrontend.Entities;
using HapetFrontend;
using HapetFrontend.Parsing.PostPrepare;
using System.Diagnostics;
using HapetFrontend.Helpers;
using HapetCompiler.ProjectConf;
using HapetCompiler.ProjectConf.Data;
using HapetCompiler.Resolvers;

namespace HapetCompiler.Toolchains
{
    internal class ProjectBuildToolchain
    {
        public CompilerSettings ProjectSettings { get; set; }
        public ProjectData ProjectData { get; set; }

		private string[] _cmdArgs; // TODO: use them for ProjectXmlParser
		public ProjectBuildToolchain(string[] args)
        {
            _cmdArgs = args;
		}

        public int Build(string projectPath, IMessageHandler messageHandler, bool referenced = false)
        {
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

            if (!referenced)
			    messageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(stopwatch.Elapsed)} Project preparation...", ReportType.Info);
			// creating settings instances for the project
			ProjectSettings = new CompilerSettings();
			// saving that the build is referenced or not
			ProjectSettings.IsReferencedCompilation = referenced;
			ProjectData = new ProjectData();
			// parsing project .hptproj file
			var projectParser = new ProjectXmlParser(projectPath, ProjectSettings, ProjectData, messageHandler);
            projectParser.PrepareProjectFile(); // setting compiler settings from project
            if (messageHandler.HasErrors)
                return (int)CompilerErrors.ProjectFileParseError; // proj file parsing errors

			// setting pointer size for the whole assembly
			Compiler.AssemblyPointerSize = ProjectSettings.TargetPlatformData.PointerSize;
            // creating the compiler and post preparer
            var compiler = new Compiler(ProjectSettings, messageHandler);
			var postPreparer = new PostPrepare(compiler);
			compiler.InitGlobalScope();
            compiler.CompilationStopwatch = stopwatch;

            // references
            ProjectReferencesResolver resolver = new ProjectReferencesResolver();
            resolver.ResolveProjectShite(ProjectData, ProjectSettings, compiler, postPreparer);

			if (!referenced)
				messageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(stopwatch.Elapsed)} Parsing...", ReportType.Info);
			// gen ast shite
			compiler.GenerateAstTree();
			if (messageHandler.HasErrors)
                return (int)CompilerErrors.ParsingError; // parsing errors

			if (!referenced)
				messageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(stopwatch.Elapsed)} Post preparation...", ReportType.Info);
			// post prepare
			int ppResult = postPreparer.StartPreparation();
            if (ppResult != 0)
				return ppResult; // post prepare errors
			if (messageHandler.HasErrors)
                return (int)CompilerErrors.PostPrepareError; // post prepare errors

			if (!referenced)
				messageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(stopwatch.Elapsed)} Code generation...", ReportType.Info);
			// code gen
			bool codeGenOk = GenerateAndCompileCode(compiler, postPreparer, resolver, messageHandler);
            if (messageHandler.HasErrors || !codeGenOk)
                return (int)CompilerErrors.CodeGenerationError; // code generation errors

			// all is ok :)
			if (!referenced)
				messageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(stopwatch.Elapsed)} Done...", ReportType.Info);
            return (int)CompilerErrors.Ok;
        }

        private static bool GenerateAndCompileCode(Compiler compiler, PostPrepare postPreparer, ProjectReferencesResolver resolver, IMessageHandler messageHandler)
        {
            var generator = new LlvmCodeGenerator();
            bool success = generator.GenerateCode(compiler, postPreparer, messageHandler);
            if (!success)
                return false;

            // TODO: config parameters normally
            return generator.CompileCode(resolver.PathsToLinkWith, resolver.LibrariesToLinkWith, messageHandler);
        }
    }
}
