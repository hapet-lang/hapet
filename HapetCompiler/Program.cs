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
            // TODO: set from args!!!
            CompilerSettings.TargetPlatformData = CompilerSettings.CurrentPlatformData;

            if (args.Length == 0)
            {
                messageHandler.ReportMessage([], ErrorCode.Get(CTEN.NoHapetCommandSpecified));
                return (int)CompilerErrors.HapetCommandError;
            }

            switch (args[0])
            {
                case "build":
                    {
                        if (args.Length == 1 && !TrySearchProjectInCurrentContext(ref args))
                        {
                            messageHandler.ReportMessage(["build"], ErrorCode.Get(CTEN.NoHapetProjectPathSpecified));
                            return (int)CompilerErrors.HapetCommandParamsError;
                        }

                        // make the stopwatch here
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();

                        // skip the first two args because they are already used
                        ProjectBuildToolchain projectToolchain = new ProjectBuildToolchain(stopwatch, args.Skip(2).ToArray());
                        return projectToolchain.Build(args[1], messageHandler, out string _);
                    }
                case "run":
                    {
                        if (args.Length == 1 && !TrySearchProjectInCurrentContext(ref args))
                        {
                            // TODO: check current folder for .hptproj existance
                            messageHandler.ReportMessage(["run"], ErrorCode.Get(CTEN.NoHapetProjectPathSpecified));
                            return (int)CompilerErrors.HapetCommandParamsError;
                        }

                        // make the stopwatch here
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();

                        // skip the first two args because they are already used
                        ProjectRunToolchain projectToolchain = new ProjectRunToolchain(stopwatch, args.Skip(2).ToArray());
                        return projectToolchain.Run(args[1], messageHandler);
                    }
                case "restore":
                    {
                        if (args.Length == 1 && !TrySearchProjectInCurrentContext(ref args))
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
                        if (args.Length == 1 && !TrySearchProjectInCurrentContext(ref args))
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
                        return await projectToolchain.WatchAsync(args[1], lspMessageHandler, messageHandler);
                    }
                case "new":
                    {
                        if (args.Length == 1)
                        {
                            messageHandler.ReportMessage(["new"], ErrorCode.Get(CTEN.NoProjectNewTypeSpecified));
                            return (int)CompilerErrors.HapetCommandParamsError;
                        }

                        // make the stopwatch here
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();

                        // skip the first two args because they are already used
                        ProjectNewToolchain projectToolchain = new ProjectNewToolchain(stopwatch, args.Skip(2).ToArray());
                        return await projectToolchain.CreateProjectAsync(args[1], messageHandler);
                    }
                case "check":
                    {
                        // make the stopwatch here
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();

                        // skip the first two args because they are already used
                        ProjectCheckToolchain projectToolchain = new ProjectCheckToolchain(stopwatch);
                        return await projectToolchain.CheckAsync(messageHandler);
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
                case "-h":
                case "--help":
                    {
                        messageHandler.ReportMessage([$"Available commands: "], null, ReportType.Info);
                        messageHandler.ReportMessage([$"  hapet build <project> <args>\t\tBuilds specified project"], null, ReportType.Info);
                        messageHandler.ReportMessage([$"  hapet run <project> <args>\t\tBuilds and runs specified project"], null, ReportType.Info);
                        messageHandler.ReportMessage([$"  hapet restore <project> <args>\tRestores dependencies of specified project"], null, ReportType.Info);
                        messageHandler.ReportMessage([$"  hapet lsp <project> <args>\t\tStarts LSP server for the project"], null, ReportType.Info);
                        messageHandler.ReportMessage([$"  hapet -v\t\t\t\tPrints version of hapet compiler"], null, ReportType.Info);
                        messageHandler.ReportMessage([$"  hapet -h\t\t\t\tPrints help"], null, ReportType.Info);
                        messageHandler.ReportMessage([$"Run 'hapet <command> --help' for more detailed info about a command."], null, ReportType.Info);
                        return 0;
                    }
            }
            messageHandler.ReportMessage([args[0]], ErrorCode.Get(CTEN.NotFoundHapetCommand));
            return (int)CompilerErrors.HapetCommandError;
        }

        /// <summary>
        /// Used by hapet commands that require project path
        /// </summary>
        /// <param name="args">Args that user passed to hapet</param>
        /// <returns>Found project in current context</returns>
        private static bool TrySearchProjectInCurrentContext(ref string[] args)
        {
            // dir where hapet is running
            string currentDirectory = Directory.GetCurrentDirectory();
            var files = Directory.GetFiles(currentDirectory, "*.hptproj", SearchOption.TopDirectoryOnly);
            if (files.Length == 0 || files.Length > 1)
                return false;

            // place project path at index 1
            string[] newArgs = new string[args.Length + 1];
            newArgs[0] = args[0];
            newArgs[1] = files[0];
            Array.Copy(args, 1, newArgs, 2, args.Length - 1);

            // replace with new args
            args = newArgs;
            return true;
        }
    }
}
