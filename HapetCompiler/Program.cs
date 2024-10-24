using HapetBackend.Llvm;
using HapetCommon;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Parsing.PostPrepare;
using System.Text;

namespace HapetCompiler
{
	public class Program
	{
		private const string _testFile = "TestFile1.5.hpt";

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

		static int Main(string[] args)
		{
			Console.OutputEncoding = Encoding.UTF8;
			var errorHandler = new ConsoleErrorHandler(0, 0, true);

			CompilerSettings.InitCurrentPlatformData();
			var projectParser = new ProjectXmlParser("../../../../../test/TestProject/TestProject.hptproj", errorHandler); // TODO: set project path here
			projectParser.UpdateSettings(); // setting compiler settings from project
			if (errorHandler.HasErrors)
			{
				return (int)CompilerErrors.ProjectFileParseError; // proj file parsing errors
			}

			var compiler = new Compiler(errorHandler);
			var postPreparer = new PostPrepare(compiler);
			errorHandler.TextProvider = compiler;

			// TODO: go all over the files and at first generate header file for the project. then parse them normally
			var ptFile = compiler.AddFile(_testFile);

			if (errorHandler.HasErrors)
			{
				return (int)CompilerErrors.ParsingError; // parsing errors
			}

			postPreparer.StartPreparation();

			if (errorHandler.HasErrors)
			{
				return (int)CompilerErrors.PostPrepareError; // post prepare errors
			}

			bool codeGenOk = GenerateAndCompileCode(compiler, errorHandler);

			if (errorHandler.HasErrors || !codeGenOk)
			{
				return (int)CompilerErrors.CodeGenerationError; // code generation errors
			}

			return (int)CompilerErrors.Ok;
		}

		private static bool GenerateAndCompileCode(Compiler compiler, IErrorHandler errorHandler)
		{
			var generator = new LlvmCodeGenerator();
			bool success = generator.GenerateCode(compiler, errorHandler, "./", "./", "TestFile", false, true);
			if (!success)
				return false;

			// TODO: config parameters normally
			return generator.CompileCode(Enumerable.Empty<string>(), Enumerable.Empty<string>(), "console", errorHandler, true);
		}
	}
}
