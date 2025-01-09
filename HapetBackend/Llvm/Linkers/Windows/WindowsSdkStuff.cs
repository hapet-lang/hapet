using HapetFrontend.Entities;
using Microsoft.Win32;
using System.Security.AccessControl;

namespace HapetBackend.Llvm.Linkers.Windows
{
    public static partial class WinLinker
    {
        internal class HapetWindowsSdk
        {
            public string Version { get; set; }
            public string Path { get; set; }
            public string UcrtPath { get; set; }
            public string UmPath { get; set; }
        }

        public static string GetLatestSdkVersion(string sdkPath, string[] skips = null)
        {
            int v0 = 0, v1 = 0, v2 = 0, v3 = 0;
            string version = null;
            foreach (var path in Directory.EnumerateDirectories(sdkPath))
            {
                var v = path.Scan1(@"(\d+).(\d+).(\d+).(\d+)").Select(s => int.TryParse(s, out int r) ? r : 0).ToArray();

                if (v.Length != 4)
                    continue;

                if (v[0] == 10 && v[1] == 0 && v[2] == 10240 && v[3] == 0)
                {
                    // Microsoft released 26624 as 10240 accidentally.
                    // https://developer.microsoft.com/en-us/windows/downloads/sdk-archive
                    v[2] = 26624;
                }

                if ((v[0] > v0) || (v[1] > v1) || (v[2] > v2) || (v[3] > v3))
                {
                    if (skips != null)
                    {
                        var tmpVers = $"{v[0]}.{v[1]}.{v[2]}.{v[3]}";
                        if (skips.Contains(tmpVers))
                            continue;
                    }
                    v0 = v[0];
                    v1 = v[1];
                    v2 = v[2];
                    v3 = v[3];

                    version = $"{v[0]}.{v[1]}.{v[2]}.{v[3]}";
                }
            }

            return version;
        }

        internal static HapetWindowsSdk FindWindowsSdk(string target, IMessageHandler messageHandler)
        {
            using (var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
            using (var roots = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Kits\Installed Roots", RegistryRights.ReadKey))
            {
                var sdk = new HapetWindowsSdk();

                sdk.Path = roots.GetValue("KitsRoot10") as string;

                if (sdk.Path == null)
                    return null;

                sdk.Version = GetLatestSdkVersion(Path.Combine(sdk.Path, "bin"));
                if (sdk.Version == null)
                    return null;

                string sdkBinPath = Path.Combine(sdk.Path, "bin");

                List<string> excludedVersions = new List<string>();
                string prevVersion = null;

                while (sdk.UcrtPath == null ||
                    !Directory.Exists(sdk.UcrtPath))
                {
                    excludedVersions.Add(prevVersion);

                    prevVersion = GetLatestSdkVersion(sdkBinPath, excludedVersions.ToArray());
                    if (prevVersion == null)
                    {
                        // error!!! 
                        messageHandler.ReportMessage($"Required path {Path.Combine(sdk.Path, "Lib", "vsversion", "ucrt", target)} could not be found");
                        break;
                    }
                    sdk.UcrtPath = Path.Combine(sdk.Path, "Lib", prevVersion, "ucrt", target);
                }

                excludedVersions.Clear();
                prevVersion = null;

                while (sdk.UmPath == null ||
                    !Directory.Exists(sdk.UmPath) ||
                    (new DirectoryInfo(sdk.UmPath)).GetFiles().FirstOrDefault(x => x.Name.Contains("kernel32")) == null) // if there is no kernel 32 lib
                {
                    excludedVersions.Add(prevVersion);

                    prevVersion = GetLatestSdkVersion(sdkBinPath, excludedVersions.ToArray());
                    if (prevVersion == null)
                    {
                        // error!!! 
                        messageHandler.ReportMessage($"Required path {Path.Combine(sdk.Path, "Lib", "vsversion", "um", target)} could not be found");
                        break;
                    }
                    sdk.UmPath = Path.Combine(sdk.Path, "Lib", prevVersion, "um", target);
                }

                return sdk;
            }
        }
    }
}
