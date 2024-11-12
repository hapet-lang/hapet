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
        private string[] _cmdArgs; // TODO: use them for ProjectXmlParser
		public ProjectBuildToolchain(string[] args)
        {
            _cmdArgs = args;
		}

        public int Build(string projectPath, IMessageHandler messageHandler)
        {
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			messageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(stopwatch.Elapsed)} Project preparation...", ReportType.Info);
			// creating settings instances for the project
			CompilerSettings currentProjectSettings = new CompilerSettings();
            ProjectData currentProjectData = new ProjectData();
			// parsing project .hptproj file
			var projectParser = new ProjectXmlParser(projectPath, currentProjectSettings, currentProjectData, messageHandler);
            projectParser.PrepareProjectFile(); // setting compiler settings from project
            if (messageHandler.HasErrors)
                return (int)CompilerErrors.ProjectFileParseError; // proj file parsing errors

            messageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(stopwatch.Elapsed)} Parsing...", ReportType.Info);
			// setting pointer size for the whole assembly
			Compiler.AssemblyPointerSize = currentProjectSettings.TargetPlatformData.PointerSize;
            // creating the compiler and post preparer
            var compiler = new Compiler(currentProjectSettings, messageHandler);
			var postPreparer = new PostPrepare(compiler);
			compiler.InitGlobalScope();
            compiler.CompilationStopwatch = stopwatch;

            // references
            ProjectReferencesResolver resolver = new ProjectReferencesResolver();
            resolver.ResolveProjectShite(currentProjectData, currentProjectSettings, compiler, postPreparer);

            // gen ast shite
            compiler.GenerateAstTree();
			if (messageHandler.HasErrors)
                return (int)CompilerErrors.ParsingError; // parsing errors

			messageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(stopwatch.Elapsed)} Post preparation...", ReportType.Info);
			// post prepare
			int ppResult = postPreparer.StartPreparation();
            if (ppResult != 0)
				return ppResult; // post prepare errors
			if (messageHandler.HasErrors)
                return (int)CompilerErrors.PostPrepareError; // post prepare errors

			messageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(stopwatch.Elapsed)} Code generation...", ReportType.Info);
			// code gen
			bool codeGenOk = GenerateAndCompileCode(compiler, postPreparer, messageHandler);
            if (messageHandler.HasErrors || !codeGenOk)
                return (int)CompilerErrors.CodeGenerationError; // code generation errors

            // all is ok :)
			messageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(stopwatch.Elapsed)} Done...", ReportType.Info);
            return (int)CompilerErrors.Ok;
        }

        private bool GenerateAndCompileCode(Compiler compiler, PostPrepare postPreparer, IMessageHandler messageHandler)
        {
            var generator = new LlvmCodeGenerator();
            bool success = generator.GenerateCode(compiler, postPreparer, messageHandler);
            if (!success)
                return false;

            // TODO: config parameters normally
            return generator.CompileCode(Enumerable.Empty<string>(), Enumerable.Empty<string>(), messageHandler);
        }
    }
}
