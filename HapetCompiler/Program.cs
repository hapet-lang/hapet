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

			// hptproj should be parsed here
			CompilerSettings.TargetPlatformData = CompilerSettings.SupportedPlatforms.FirstOrDefault(x => x.TargetPlatform == TargetPlatform.Win86);
			CompilerSettings.TargetFormat = TargetFormat.Console;
			CompilerSettings.InitCurrentPlatformData();

			var errorHandler = new ConsoleErrorHandler(0, 0, true);
			var compiler = new Compiler(errorHandler);
			var postPreparer = new PostPrepare(compiler);
			errorHandler.TextProvider = compiler;

			var ptFile = compiler.AddFile(_testFile);

			if (errorHandler.HasErrors)
			{
				return 1; // parsing errors
			}

			postPreparer.StartPreparation();

			if (errorHandler.HasErrors)
			{
				return 2; // post prepare errors
			}

			bool codeGenOk = GenerateAndCompileCode(compiler, errorHandler);

			if (errorHandler.HasErrors || !codeGenOk)
			{
				return 3; // code generation errors errors
			}

			return 0;
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
