using Frontend;
using Frontend.Visitors;
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

			var errorHandler = new ConsoleErrorHandler(0, 0, true);
			var compiler = new Compiler(errorHandler);
			errorHandler.TextProvider = compiler;

			var ptFile = compiler.AddFile(_testFile);
			compiler.CompileAll();

			var printer = new AnalysedAstPrinter();
			using (var file = File.Open("analysed.hpt", FileMode.Create))
			using (var writer = new StreamWriter(file))
			{
				printer.PrintWorkspace(compiler, writer);
			}
		}
	}
}
