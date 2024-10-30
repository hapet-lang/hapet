using HapetBackend.Llvm;
using HapetFrontend.Entities;
using HapetFrontend;
using HapetFrontend.Parsing.PostPrepare;

namespace HapetCompiler.Toolchains
{
    class CompilationResult
    {
        public int ExitCode;
        public TimeSpan? LexAndParse;
        public TimeSpan? SemanticAnalysis;
        public TimeSpan? FrontEnd;
        public TimeSpan? BackEnd;
        public TimeSpan? Execution;
        public bool PrintTime = false;
    }

    internal class ProjectBuildToolchain
    {
        private string[] _cmdArgs; // TODO: use them for ProjectXmlParser
		public ProjectBuildToolchain(string[] args)
        {
            _cmdArgs = args;
		}

        public int Build(string projectPath, IErrorHandler errorHandler)
        {
            // creating settings instance for the project
            CompilerSettings currentProjectSettings = new CompilerSettings();

            // parsing project .hptproj file
            var projectParser = new ProjectXmlParser(projectPath, currentProjectSettings, errorHandler); // TODO: set project path here
            projectParser.UpdateSettings(); // setting compiler settings from project
            if (errorHandler.HasErrors)
            {
                return (int)CompilerErrors.ProjectFileParseError; // proj file parsing errors
            }

            // setting pointer size for the whole assembly
            Compiler.AssemblyPointerSize = currentProjectSettings.TargetPlatformData.PointerSize;

            // creating the compiler and post preparer
            var compiler = new Compiler(currentProjectSettings, errorHandler);
            compiler.InitGlobalScope();
            var postPreparer = new PostPrepare(compiler);
            errorHandler.TextProvider = compiler;

            // TODO: go all over the files and at first generate header file for the project. then parse them normally
            // var ptFile = compiler.AddFile(_testFile);
            var allFilesInProjectFolder = (new DirectoryInfo(Path.GetDirectoryName(currentProjectSettings.ProjectPath))).EnumerateFiles("*", SearchOption.AllDirectories);
            foreach (var file in allFilesInProjectFolder)
            {
                if (Path.GetExtension(file.FullName) == ".hpt")
				    compiler.AddFile(file.FullName);
			}

            if (errorHandler.HasErrors)
            {
                return (int)CompilerErrors.ParsingError; // parsing errors
            }

            // post prepare
            postPreparer.StartPreparation();
            if (errorHandler.HasErrors)
            {
                return (int)CompilerErrors.PostPrepareError; // post prepare errors
            }

            // code gen
            bool codeGenOk = GenerateAndCompileCode(compiler, postPreparer, errorHandler);
            if (errorHandler.HasErrors || !codeGenOk)
            {
                return (int)CompilerErrors.CodeGenerationError; // code generation errors
            }

            return (int)CompilerErrors.Ok;
        }

        private bool GenerateAndCompileCode(Compiler compiler, PostPrepare postPreparer, IErrorHandler errorHandler)
        {
            var generator = new LlvmCodeGenerator();
            bool success = generator.GenerateCode(compiler, postPreparer, errorHandler);
            if (!success)
                return false;

            // TODO: config parameters normally
            return generator.CompileCode(Enumerable.Empty<string>(), Enumerable.Empty<string>(), errorHandler);
        }
    }
}
