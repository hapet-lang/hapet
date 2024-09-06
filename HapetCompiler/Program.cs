using HapetFrontend;
using HapetFrontend.Parsing.PostPrepare;
using System.Text;

namespace HapetCompiler
{
	public class Program
	{
		private const string _testFile = "TestFile1.hpt";

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

		static void Main(string[] args)
		{
			Console.OutputEncoding = Encoding.UTF8;

			// hptproj should be parsed here
			CompilerSettings.PlatformData = CompilerSettings.SupportedPlatforms.FirstOrDefault();

			var errorHandler = new ConsoleErrorHandler(0, 0, true);
			var compiler = new Compiler(errorHandler);
			var postPreparer = new PostPrepare(compiler);
			errorHandler.TextProvider = compiler;

			var ptFile = compiler.AddFile(_testFile);

			postPreparer.StartPreparation();

			//var printer = new AnalysedAstPrinter();
			//using (var file = File.Open("analysed.hpt", FileMode.Create))
			//using (var writer = new StreamWriter(file))
			//{
			//	printer.PrintWorkspace(compiler, writer);
			//}
		}
	}
}
