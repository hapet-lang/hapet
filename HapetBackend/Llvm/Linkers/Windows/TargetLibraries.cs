using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetBackend.Llvm.Linkers.Windows
{
    public partial class WinLinker
    {
        private bool LinkPlatformLibraries(Compiler compiler, List<string> lldArgs, IMessageHandler messageHandler, string target)
        {
            // platform libraries
            switch (compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform)
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
                                }

                        }

                        // windows and c libs
                        //lldArgs.Add("libucrtd.lib");
                        //lldArgs.Add("libcmtd.lib");

                        //lldArgs.Add("kernel32.lib");
                        //lldArgs.Add("user32.lib");
                        //lldArgs.Add("gdi32.lib");
                        //lldArgs.Add("winspool.lib");
                        //lldArgs.Add("comdlg32.lib");
                        //lldArgs.Add("advapi32.lib");
                        //lldArgs.Add("shell32.lib");
                        //lldArgs.Add("ole32.lib");
                        //lldArgs.Add("oleaut32.lib");
                        //lldArgs.Add("uuid.lib");
                        //lldArgs.Add("odbc32.lib");
                        //lldArgs.Add("odbccp32.lib");

                        //lldArgs.Add("legacy_stdio_definitions.lib");
                        //lldArgs.Add("legacy_stdio_wide_specifiers.lib");
                        // lldArgs.Add("libclang.lib");
                        // lldArgs.Add("libvcruntimed.lib");
                        lldArgs.Add("msvcrt.lib");
                        lldArgs.Add("ucrt.lib");
                        //lldArgs.Add("vcruntime.lib");
                        //lldArgs.Add("libcmt.lib");
                        //lldArgs.Add("libcmt.lib");
                        //lldArgs.Add("shlwapi.lib");
                        break;
                    }
            }
            return true;
        }
    }
}
