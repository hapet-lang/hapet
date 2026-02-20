using HapetCompiler.Toolchains;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetLsp;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Text;

namespace HapetCompiler
{
    public class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var messageHandler = new ConsoleMessageHandler(0, 0, true);
            CompilerSettings.InitCurrentPlatformData();

            if (args.Length == 0)
            {
                messageHandler.ReportMessage([], ErrorCode.Get(CTEN.NoHapetCommandSpecified));
                return (int)CompilerErrors.HapetCommandError;
            }

            switch (args[0])
            {
                case "build":
                    {
                        if (args.Length == 1)
                        {
                            messageHandler.ReportMessage(["build"], ErrorCode.Get(CTEN.NoHapetProjectPathSpecified));
                            return (int)CompilerErrors.HapetCommandParamsError;
                        }

                        // make the stopwatch here
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();

                        // skip the first two args because they are already used
                        ProjectBuildToolchain projectToolchain = new ProjectBuildToolchain(stopwatch, args.Skip(2).ToArray());
                        return projectToolchain.Build(args[1], messageHandler);
                    }
                case "restore":
                    {
                        if (args.Length == 1)
                        {
                            messageHandler.ReportMessage(["restore"], ErrorCode.Get(CTEN.NoHapetProjectPathSpecified));
                            return (int)CompilerErrors.HapetCommandParamsError;
                        }
                        // skip the first two args because they are already used
                        ProjectRestoreToolchain projectToolchain = new ProjectRestoreToolchain(args.Skip(2).ToArray());
                        return projectToolchain.Restore(args[1], messageHandler);
                    }
                case "lsp":
                    {
                        if (args.Length == 1)
                        {
                            messageHandler.ReportMessage(["lsp"], ErrorCode.Get(CTEN.NoHapetProjectPathSpecified));
                            return (int)CompilerErrors.HapetCommandParamsError;
                        }

                        // make the stopwatch here
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();

                        // skip the first two args because they are already used
                        ProjectLspToolchain projectToolchain = new ProjectLspToolchain(stopwatch, args.Skip(2).ToArray());
                        LspMessageHandler lspMessageHandler = new LspMessageHandler();
                        return await projectToolchain.WatchAsync(args[1], lspMessageHandler);
                    }
                case "-v":
                case "--version":
                    {
                        string versionFilePath = Path.Combine(CompilerUtils.CurrentHapetDirectory, "version.txt");
                        if (!File.Exists(versionFilePath))
                        {
                            messageHandler.ReportMessage([], ErrorCode.Get(CTEN.NoVersionFileFound));
                            return (int)CompilerErrors.HapetCommandParamsError;
                        }

                        string version = File.ReadAllText(versionFilePath);
                        messageHandler.ReportMessage([$"Hapet compiler version: {version}"], null, ReportType.Info);
                        return 0;
                    }
            }
            messageHandler.ReportMessage([args[0]], ErrorCode.Get(CTEN.NotFoundHapetCommand));
            return (int)CompilerErrors.HapetCommandError;
        }
    }
}
