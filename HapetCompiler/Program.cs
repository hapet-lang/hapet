using HapetCompiler.Toolchains;
using HapetFrontend;
using System.Text;

namespace HapetCompiler
{
    public class Program
	{
		static int Main(string[] args)
		{
			Console.OutputEncoding = Encoding.UTF8;
			var errorHandler = new ConsoleErrorHandler(0, 0, true);
			CompilerSettings.InitCurrentPlatformData();
			
			ProjectBuildToolchain projectToolchain = new ProjectBuildToolchain();
			return projectToolchain.Build("../../../../../test/TestProject/TestProject.hptproj", errorHandler);
		}
	}
}
