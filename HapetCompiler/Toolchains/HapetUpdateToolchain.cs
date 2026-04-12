using HapetCommon;
using HapetCommon.Web;
using HapetCommon.Web.Requests;
using HapetFrontend;
using HapetFrontend.Entities;
using HashComputer.Backend.Entities;
using HashComputer.Backend.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

namespace HapetCompiler.Toolchains
{
    internal sealed class HapetUpdateToolchain
    {
        private readonly Stopwatch _stopwatch;
        private readonly string[] _cmdArgs;
        public HapetUpdateToolchain(Stopwatch stopwatch, string[] args)
        {
            _stopwatch = stopwatch;
            _cmdArgs = args;
        }

        async public Task<int> TryUpdateHapetAsync(IMessageHandler messageHandler, CancellationToken cancellationToken)
        {
            // check for --help
            if (_cmdArgs.Contains("--help") || _cmdArgs.Contains("-h"))
            {
                // print help
                PrintHelp(messageHandler);
                return 0;
            }

            // check for manual update
            bool isManualUpdate = _cmdArgs.Contains("--manual");

            messageHandler.ReportMessage($"Checking for updates. Consider to close all applications that may block hapet files from updating.");

            using var webSevice = new WebService(messageHandler);
            var (isAvailable, newVersion) = await IsUpdateAvailableAsync(webSevice, messageHandler);
            if (isAvailable)
            {
                messageHandler.ReportMessage($"Update available. New version of hapet is {newVersion}, starting download...");
                await DownloadUpdateAsync(webSevice, messageHandler, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return -1;
                UpdateUpdater(messageHandler);
                if (cancellationToken.IsCancellationRequested) return -1;

                if (isManualUpdate)
                {
                    messageHandler.ReportMessage($"Download complete. Run 'hapet-replacer' to replace compiler with new updated files.");
                    return 0;
                }

                RunUpdater(messageHandler);
                if (cancellationToken.IsCancellationRequested) return -1;

                messageHandler.ReportMessage($"Hapet updated succesfully in {_stopwatch.Elapsed.TotalSeconds:F2} seconds.");
                messageHandler.ReportMessage($"Current version of hapet is {newVersion}.");
                messageHandler.ReportMessage($"Please wait for a few seconds before using hapet again because new binary files are going to be copied now.");
                Environment.Exit(0);
            }
            messageHandler.ReportMessage($"The latest version of hapet is already installed.");
            return 0;
        }

        #region Updater shite
        async private Task<(bool isAvailable, string version)> IsUpdateAvailableAsync(WebService webSevice, IMessageHandler messageHandler)
        {
            string hapetPath = CompilerUtils.CurrentHapetDirectory;
            string hashFilePath = Path.Combine(hapetPath, CompilerUtils.TMP_COMPUTED_HASH_FILENAME);
            string existedHashFilePath = Path.Combine(hapetPath, CompilerUtils.COMPUTED_HASH_FILENAME);
            string platformFolderName = CompilerSettings.CurrentPlatformData.Name;

            // gettings hashes from server
            var result = await webSevice.ExecuteRequestTaskAsync(new FileRequest($"{CompilerUtils.HAPET_DOWNLOAD_LINK}/{platformFolderName}/{CompilerUtils.COMPUTED_HASH_FILENAME}", hashFilePath, null));
            if (result.IsExecutedNormally)
            {
                try
                {
                    var hashes = JsonSerializer.Deserialize<ComputedHashJson>(File.ReadAllText(hashFilePath));
                    if (hashes == null)
                    {
                        // if there is no hash file on server - there is nothing to do then
                        return (false, "---");
                    }

                    // remove tmp hashes
                    if (File.Exists(hashFilePath))
                        File.Delete(hashFilePath);

                    // gettings existed hashes on update to compare them
                    ComputedHashJson existedHashes = new ComputedHashJson();
                    if (File.Exists(existedHashFilePath))
                    {
                        existedHashes = JsonSerializer.Deserialize<ComputedHashJson>(File.ReadAllText(existedHashFilePath));
                        return (existedHashes.Version != hashes.Version, hashes.Version);
                    }
                    return (true, hashes.Version);
                }
                catch (TaskCanceledException)
                {
                    messageHandler.ReportMessage($"Update canceled");
                }
                catch (Exception ex)
                {
                    messageHandler.ReportMessage($"Error while checking for update: {ex}");
                }
            }
            return (false, "--");
        }

        async private Task DownloadUpdateAsync(WebService webSevice, IMessageHandler messageHandler, CancellationToken cancellationToken)
        {
            string hapetPath = CompilerUtils.CurrentHapetDirectory;
            string hashFilePath = Path.Combine(hapetPath, CompilerUtils.TMP_COMPUTED_HASH_FILENAME);
            string existedHashFilePath = Path.Combine(hapetPath, CompilerUtils.COMPUTED_HASH_FILENAME);
            string platformFolderName = CompilerSettings.CurrentPlatformData.Name;
            string tmpHapetDir = Path.Combine(hapetPath, CompilerUtils.HAPET_TEMP_UPDATE_FOLDER);

            // remove everything before updating
            CompilerUtils.DeleteEverythingUnderDirectory(tmpHapetDir);

            // gettings hashes from server
            var result = await webSevice.ExecuteRequestTaskAsync(new FileRequest($"{CompilerUtils.HAPET_DOWNLOAD_LINK}/{platformFolderName}/{CompilerUtils.COMPUTED_HASH_FILENAME}", hashFilePath, null));
            if (result.IsExecutedNormally)
            {
                try
                {
                    var hashes = JsonSerializer.Deserialize<ComputedHashJson>(File.ReadAllText(hashFilePath));
                    // there iis nothing to do if server hashes are null
                    if (hashes == null)
                    {
                        return;
                    }

                    // gettings existed hashes on update to compare them
                    ComputedHashJson existedHashes = new ComputedHashJson();
                    // files that has to be force updated
                    // because of the changed hashes of them on the computer
                    List<string> forceUpdate = new List<string>();
                    if (File.Exists(existedHashFilePath))
                    {
                        // validating
                        existedHashes = JsonSerializer.Deserialize<ComputedHashJson>(File.ReadAllText(existedHashFilePath));

                        // if need to check real files 
                        ComputedHashJson realLocalHash = await (new ComputerService()).ComputeHashPure(new HashComputer.Backend.ComputeParameters()
                        {
                            Path = hapetPath,
                            TaskNumber = 8,
                        });

                        foreach (var hash in existedHashes.ComputedHashes)
                        {
                            // local file was removed - reload it
                            if (!realLocalHash.ComputedHashes.ContainsKey(hash.Key))
                            {
                                forceUpdate.Add(hash.Key);
                                continue;
                            }

                            // local file has been changed - reload it
                            if (realLocalHash.ComputedHashes[hash.Key] != hash.Value)
                            {
                                forceUpdate.Add(hash.Key);
                                continue;
                            }
                        }
                    }

                    // download them file by file
                    var computedHashes = hashes.ComputedHashes.ToArray();
                    for (int i = 0; i < computedHashes.Length; ++i)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        var pair = computedHashes[i];
                        // if the hashes are equal - no need to download (except the forceUpdate)
                        if (existedHashes.ComputedHashes != null &&
                            existedHashes.ComputedHashes.ContainsKey(pair.Key) &&
                            existedHashes.ComputedHashes[pair.Key] == pair.Value &&
                            !forceUpdate.Contains(pair.Key))
                            continue;

                        // creating subdirectory for the file
                        var fileDir = Path.GetDirectoryName(pair.Key);
                        Directory.CreateDirectory($"{tmpHapetDir}/{fileDir}");

                        // download
                        string fileLink = $"{CompilerUtils.HAPET_DOWNLOAD_LINK}/{platformFolderName}/{pair.Key}";
                        string fileOutDir = $"{tmpHapetDir}/{pair.Key}";
                        await webSevice.DownloadFile(fileLink, fileOutDir, cancellationToken);
                    }

                    // download the hashes normally and replace existing
                    await webSevice.DownloadFile($"{CompilerUtils.HAPET_DOWNLOAD_LINK}/{platformFolderName}/{CompilerUtils.COMPUTED_HASH_FILENAME}", existedHashFilePath, cancellationToken);
                }
                catch (TaskCanceledException) 
                {
                    messageHandler.ReportMessage($"Update canceled");
                }
                catch (Exception ex)
                {
                    messageHandler.ReportMessage($"Error while downloading hapet: {ex}");
                }
            }

            // remove tmp hashes
            if (File.Exists(hashFilePath))
                File.Delete(hashFilePath);
        }

        private void UpdateUpdater(IMessageHandler messageHandler)
        {
            string hapetPath = CompilerUtils.CurrentHapetDirectory;
            string tmpHapetDir = Path.Combine(hapetPath, CompilerUtils.HAPET_TEMP_UPDATE_FOLDER);

            if (!Directory.Exists(tmpHapetDir))
                return;

            hapetPath = hapetPath.TrimEnd('/').TrimEnd('\\');
            tmpHapetDir = tmpHapetDir.TrimEnd('/').TrimEnd('\\');

            var items = Directory.GetFiles(tmpHapetDir, "*", SearchOption.AllDirectories);
            int count = items.Length;
            for (int i = 0; i < count; ++i)
            {
                var item = items[i];
                string relat = Path.GetRelativePath(tmpHapetDir, item).Trim('/').Trim('\\');

                if (!relat.Contains(CompilerUtils.UPDATER_FILE_NAME))
                    continue;

                if (CompilerSettings.Verbose)
                    messageHandler.ReportMessage($"Updating {CompilerUtils.UPDATER_FILE_NAME}... Real path: {item}, Relative: {relat}");

                string dst = $"{hapetPath}/{relat}";
                try
                {
                    string dr = Path.GetDirectoryName(dst);
                    if (!Directory.Exists(dr))
                        Directory.CreateDirectory(dr);
                    File.Copy(item, dst, true);
                }
                catch (TaskCanceledException)
                {
                    messageHandler.ReportMessage($"Update canceled");
                }
                catch (Exception e)
                {
                    messageHandler.ReportMessage($"Error while swapping the file: {item}: {e}\n");
                }
            }

            if (CompilerSettings.Verbose)
                messageHandler.ReportMessage($"Done updating {CompilerUtils.UPDATER_FILE_NAME}");
        }

        private void RunUpdater(IMessageHandler messageHandler)
        {
            string hapetPath = CompilerUtils.CurrentHapetDirectory;
            string updaterPath = Path.Combine(hapetPath, 
                $"{CompilerUtils.UPDATER_FILE_NAME}{CompilerSettings.CurrentPlatformData.ExecutableFileExtension}");

            if (CompilerSettings.Verbose)
                messageHandler.ReportMessage($"Trying to run updater under the path: {updaterPath}");
            if (File.Exists(updaterPath))
            {
                var process = new Process();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    process.StartInfo.FileName = updaterPath;
                    process.StartInfo.Arguments = Environment.ProcessId.ToString();
                    process.StartInfo.Verb = "runas";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();

                    if (CompilerSettings.Verbose)
                        messageHandler.ReportMessage($"Windows updater started by path: {updaterPath}");
                }
                else
                {
                    // TODO: pass PID as arg for updater
                    process.StartInfo.FileName = "/bin/bash";
                    process.StartInfo.Arguments = string.Format("-c \"{0}\"", updaterPath);
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();

                    if (CompilerSettings.Verbose)
                        messageHandler.ReportMessage($"Linux/MacOS updater started by path: {updaterPath}");
                }
            }
        }
        #endregion

        private void PrintHelp(IMessageHandler messageHandler)
        {
            messageHandler.ReportMessage($"Usage: \n  hapet update <args> \n");
            messageHandler.ReportMessage($"Parameters: ");
            messageHandler.ReportMessage($"  --manual \t\t With this parameter files replacer program won't be started after downloading update.");
            messageHandler.ReportMessage($"\t\t\t You should manually start it by typing 'hapet-replacer'.");
        }
    }
}
