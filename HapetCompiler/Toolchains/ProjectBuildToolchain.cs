using HapetBackend.Llvm;
using HapetFrontend.Entities;
using HapetFrontend;
using HapetFrontend.Parsing.PostPrepare;
using System.Diagnostics;

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

			messageHandler.ReportMessage($"{CompilerUtils.GetPrettyTimeString(stopwatch.Elapsed)} Project preparation...", ReportType.Info);

			// creating settings instance for the project
			CompilerSettings currentProjectSettings = new CompilerSettings();

            // parsing project .hptproj file
            var projectParser = new ProjectXmlParser(projectPath, currentProjectSettings, messageHandler);
            projectParser.UpdateSettings(); // setting compiler settings from project
            if (messageHandler.HasErrors)
            {
                return (int)CompilerErrors.ProjectFileParseError; // proj file parsing errors
            }

            messageHandler.ReportMessage($"{CompilerUtils.GetPrettyTimeString(stopwatch.Elapsed)} Parsing...", ReportType.Info);

			// setting pointer size for the whole assembly
			Compiler.AssemblyPointerSize = currentProjectSettings.TargetPlatformData.PointerSize;

            // creating the compiler and post preparer
            var compiler = new Compiler(currentProjectSettings, messageHandler);
            compiler.InitGlobalScope();
            compiler.CompilationStopwatch = stopwatch;
            var postPreparer = new PostPrepare(compiler);
			messageHandler.TextProvider = compiler;

            var allFilesInProjectFolder = (new DirectoryInfo(Path.GetDirectoryName(currentProjectSettings.ProjectPath))).EnumerateFiles("*", SearchOption.AllDirectories);
            foreach (var file in allFilesInProjectFolder)
            {
                if (Path.GetExtension(file.FullName) == ".hpt")
				    compiler.AddFile(file.FullName);
			}
            if (messageHandler.HasErrors)
            {
                return (int)CompilerErrors.ParsingError; // parsing errors
            }

			messageHandler.ReportMessage($"{CompilerUtils.GetPrettyTimeString(stopwatch.Elapsed)} Post preparation...", ReportType.Info);

			// post prepare
			postPreparer.StartPreparation();
            if (messageHandler.HasErrors)
            {
                return (int)CompilerErrors.PostPrepareError; // post prepare errors
            }

			messageHandler.ReportMessage($"{CompilerUtils.GetPrettyTimeString(stopwatch.Elapsed)} Code generation...", ReportType.Info);

			// code gen
			bool codeGenOk = GenerateAndCompileCode(compiler, postPreparer, messageHandler);
            if (messageHandler.HasErrors || !codeGenOk)
            {
                return (int)CompilerErrors.CodeGenerationError; // code generation errors
            }

			messageHandler.ReportMessage($"{CompilerUtils.GetPrettyTimeString(stopwatch.Elapsed)} Done...", ReportType.Info);


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
