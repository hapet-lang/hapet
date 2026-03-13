using HapetBackend.Llvm.Linkers;
using HapetBackend.Llvm.Linkers.Windows;
using HapetFrontend;
using HapetFrontend.Entities;
using System.Diagnostics;

namespace HapetCompiler.Toolchains
{
    internal sealed class ProjectCheckToolchain
    {
        private readonly Stopwatch _stopwatch;
        public ProjectCheckToolchain(Stopwatch stopwatch)
        {
            _stopwatch = stopwatch;
        }

        async public Task<int> CheckAsync(IMessageHandler messageHandler)
        {
            await Task.CompletedTask;
#if DEBUG
            messageHandler.ReportMessage([$"Check should NOT be called in DEBUG mode"], null, ReportType.Error);
            return -1;
#endif

            bool isOk = true; // ok by default
            ILinker linker;
            switch (CompilerSettings.CurrentPlatformData.TargetPlatform)
            {
                case TargetPlatform.Win86:
                case TargetPlatform.Win64:
                    {
                        linker = new WinLinker();
                        break;
                    }
                default:
                    throw new NotImplementedException();
            }
            // check for linker and libs
            if (!linker.CheckLinkerAndLibs(messageHandler)) isOk = false;

            // result message
            if (isOk) messageHandler.ReportMessage([$"Result: hapet is OK"], null, ReportType.Info);
            else messageHandler.ReportMessage([$"Result: found some problems with hapet"], null, ReportType.Error);

            messageHandler.ReportMessage([$"Check done in {_stopwatch.Elapsed.TotalSeconds:F2} seconds"], null, ReportType.Info);

            return isOk ? 0 : -1;
        }
    }
}
