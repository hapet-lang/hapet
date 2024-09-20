using HapetCommon;
using HapetFrontend;
using HapetFrontend.Entities;

namespace HapetBackend.Llvm.Linkers.Windows
{
	public static partial class WinLinker
	{
		private static bool LinkPlatformLibraries(List<string> lldArgs, IErrorHandler errorHandler, string target)
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
									var winSdk = FindWindowsSdk();
									if (winSdk == null)
									{
										errorHandler.ReportError("Couldn't find windows sdk");
										return false;
									}

									var msvcLibPath = FindVisualStudioLibraryDirectory();
									if (msvcLibPath == null)
									{
										errorHandler.ReportError("Couldn't find Visual Studio library directory");
										return false;
									}
									if (winSdk.UcrtPath != null)
										lldArgs.Add($@"-libpath:{winSdk.UcrtPath}\{target}");

									if (winSdk.UmPath != null)
										lldArgs.Add($@"-libpath:{winSdk.UmPath}\{target}");
									if (msvcLibPath != null)
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
						lldArgs.Add("msvcrtd.lib");
						//lldArgs.Add("shlwapi.lib");
						break;
					}
			}
			return true;
		}
	}
}
