using HapetFrontend;
using HapetFrontend.Helpers;
using HapetFrontend.Extensions;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace HapetBackend.Llvm.Linkers.Windows
{
    public partial class WinLinker
    {
#pragma warning disable CA1812 // Class is not instantiated
        internal sealed class VsWhere
        {
            public string instanceId { get; set; }
            public string installDate { get; set; }
            public string installationName { get; set; }
            public string installationPath { get; set; }
            public string installationVersion { get; set; }
            public bool isPrerelease { get; set; }
            public string displayName { get; set; }
            public string description { get; set; }
            public string enginePath { get; set; }
            public string channelId { get; set; }
            public string channelPath { get; set; }
            public string channelUri { get; set; }
            public string releaseNotes { get; set; }
            public string thirdPartyNotices { get; set; }
        }
#pragma warning restore CA1812 // Class is not instantiated

        private (int, string) FindVSInstallDirWithVsWhere(int skipLatest = 0)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                var programFilesX86 = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%");
                var p = Funcad.StartProcess($@"{programFilesX86}\Microsoft Visual Studio\Installer\vswhere.exe", "-nologo -format json", stdout:
                    (sender, e) =>
                    {
                        sb.AppendLine(e.Data);
                    });
                p.WaitForExit();

                var versions = JsonSerializer.Deserialize<VsWhere[]>(sb.ToString());

                if (versions == null || versions.Length == 0)
                    return (-1, null);

                if (versions.Length <= skipLatest)
                    skipLatest = versions.Length - 1;

                var latest = versions.Skip(Math.Min(skipLatest, versions.Length - 1)).First();
                if (CompilerSettings.Verbose)
                    System.Console.WriteLine($"vs version: {latest.installationVersion}");

                var v = latest.installationVersion.Scan1(@"(\d+)\.(\d+)\.(\d+)\.(\d+)").Select(s => int.TryParse(s, out int i) ? i : 0).First();
                var dir = latest.installationPath;

                return (v, dir);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                return (-1, null);
            }
        }

        private static (int, string) FindVSInstallDirWithRegistry()
        {
            return (-1, null);
        }

        private static (int, string) FindVSInstallDirWithPath()
        {
            return (-1, null);
        }

        private static string FindVSLibDir(int version, string installDir, bool searchBin = false)
        {
            switch (version)
            {
                // visual studio 2017
                case 15:
                // visual studio 2019
                case 16:
                // visual studio 2022
                case 17:
                    {
                        var MSVC = Path.Combine(installDir, "VC", "Tools", "MSVC");
                        if (!Directory.Exists(MSVC))
                            return null;

                        SortedList<int[], string> versions = new SortedList<int[], string>(Comparer<int[]>.Create((a, b) =>
                        {
                            for (int i = 0; i < a.Length && i < b.Length; i++)
                            {
                                if (a[i] > b[i])
                                    return -1;
                                else if (a[i] < b[i])
                                    return 1;
                            }

                            return 0;
                        }));

                        foreach (var sub in Directory.EnumerateDirectories(MSVC))
                        {
                            var name = sub.Substring(sub.LastIndexOfAny(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) + 1);
                            var v = name.Scan1(@"(\d+)\.(\d+)\.(\d+)").Select(s => int.TryParse(s, out int i) ? i : 0).ToArray();

                            if (v.Length != 3)
                                continue;

                            versions.Add(v, sub);
                        }

                        foreach (var kv in versions)
                        {
                            var libDir = Path.Combine(kv.Value, (searchBin ? "bin" : "lib"));

                            if (!Directory.Exists(libDir))
                                continue;

                            return libDir;
                        }

                        return null;
                    }

                default:
                    return null;
            }
        }

        public string FindVisualStudioLibraryDirectory()
        {
            var (version, dir) = FindVSInstallDirWithVsWhere();

            if (dir == null)
                (version, dir) = FindVSInstallDirWithRegistry();

            if (dir == null)
                (version, dir) = FindVSInstallDirWithPath();

            if (dir == null)
                return null;

            return FindVSLibDir(version, dir);
        }

        public string FindVisualStudioBinaryDirectory()
        {
            var (version, dir) = FindVSInstallDirWithVsWhere();

            if (dir == null)
                (version, dir) = FindVSInstallDirWithRegistry();

            if (dir == null)
                (version, dir) = FindVSInstallDirWithPath();

            if (dir == null)
                return null;

            return FindVSLibDir(version, dir, true);
        }
    }
}
