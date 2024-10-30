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
			var messageHandler = new ConsoleMessageHandler(0, 0, true);
			CompilerSettings.InitCurrentPlatformData();
			
			if (args.Length == 0)
			{
				messageHandler.ReportMessage("hapet command must be specified. For example 'hapet build ...");
				return (int)CompilerErrors.HapetCommandError;
			}

			switch (args[0])
			{
				case "build":
					{
						if (args.Length == 1)
						{
							messageHandler.ReportMessage("Path to the project file must be specified. For example 'hapet build /path/to/project.hptproj'");
							return (int)CompilerErrors.HapetCommandParamsError;
						}
						// skip the first two args because they are already used
						ProjectBuildToolchain projectToolchain = new ProjectBuildToolchain(args.Skip(2).ToArray());
						return projectToolchain.Build(args[1], messageHandler);
					}
				case "restore":
					{
						if (args.Length == 1)
						{
							messageHandler.ReportMessage("Path to the project file must be specified. For example 'hapet restore /path/to/project.hptproj'");
							return (int)CompilerErrors.HapetCommandParamsError;
						}
						// skip the first two args because they are already used
						ProjectRestoreToolchain projectToolchain = new ProjectRestoreToolchain(args.Skip(2).ToArray());
						return projectToolchain.Restore(args[1], messageHandler);
					}
			}
			messageHandler.ReportMessage($"hapet command called {args[0]} is not available");
			return (int)CompilerErrors.HapetCommandError;
		}
	}
}
