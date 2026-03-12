using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using System;

namespace HapetBackend.Llvm.Linkers.Windows
{
    public partial class WinLinker
    {
        private bool LinkPlatformLibraries(Compiler compiler, List<string> lldArgs, IMessageHandler messageHandler, string target)
        {
            // platform libraries
            switch (CompilerSettings.TargetPlatformData.TargetPlatform)
            {
                case TargetPlatform.Win86:
                case TargetPlatform.Win64:
                    {
                        // if we are on win and targeting win - just use VS binaries
                        switch (CompilerSettings.CurrentPlatformData.TargetPlatform)
                        {
                            case TargetPlatform.Win86:
                            case TargetPlatform.Win64:
                                {
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
                                    break;
#else
                                    string compilerDir = CompilerUtils.CurrentHapetDirectory.Replace("\\", "/").TrimEnd('/');
                                    lldArgs.Add($@"-libpath:{compilerDir}/libs/win-x64/");
                                    break;
#endif
                                }
                        }

                        // windows and c libs
                        lldArgs.Add("msvcrt.lib");
                        lldArgs.Add("ucrt.lib");
                        break;
                    }
            }
            return true;
        }
    }
}
