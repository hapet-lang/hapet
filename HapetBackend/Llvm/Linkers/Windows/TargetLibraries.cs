using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using System;

namespace HapetBackend.Llvm.Linkers.Windows
{
    public partial class WinLinker
    {
        private static readonly string[] _winRequiredLibs = 
        { 
            "msvcrt.lib", "ucrt.lib" 
        };
        private bool LinkPlatformLibraries(Compiler compiler, List<string> lldArgs, IMessageHandler messageHandler, string target)
        {
            // platform libraries
            switch (CompilerSettings.TargetPlatformData.TargetPlatform)
            {
                case TargetPlatform.Win86:
                case TargetPlatform.Win64:
                    {
                        // if we are on win and targeting win - just use VS binaries
#if DEBUG
                        var winSdk = FindWindowsSdk(target, messageHandler);
                        if (winSdk == null)
                        {
                            messageHandler.ReportMessage([], ErrorCode.Get(CTEN.NoWindowsSdk));
                            return false;
                        }
                        if (winSdk.UcrtPath != null)
                            lldArgs.Add($@"-libpath:{winSdk.UcrtPath}");
                        if (winSdk.UmPath != null)
                            lldArgs.Add($@"-libpath:{winSdk.UmPath}");

                        var msvcLibPath = FindVisualStudioLibraryDirectory();
                        if (msvcLibPath == null)
                        {
                            messageHandler.ReportMessage([], ErrorCode.Get(CTEN.NoVisualStudioLib));
                            return false;
                        }

                        lldArgs.Add($@"-libpath:{msvcLibPath}\{target}");
#else
                        string compilerDir = CompilerUtils.CurrentHapetDirectory.Replace("\\", "/").TrimEnd('/');
                        lldArgs.Add($@"-libpath:{compilerDir}/libs/win-x64/");
#endif

                        // windows and c libs
                        foreach (var l in _winRequiredLibs) lldArgs.Add(l);
                        break;
                    }
            }
            return true;
        }
    }
}
