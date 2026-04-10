using HapetCommon;
using HapetCommon.Web;
using HapetCommon.Web.Requests;
using HapetFrontend;
using HapetFrontend.Entities;
using HashComputer.Backend.Entities;
using System.Diagnostics;

namespace HapetCompiler.Toolchains
{
    internal sealed class HapetUpdateToolchain
    {
        private const string HAPET_DOWNLOAD_LINK = "https://hapetlang.com/resources/hapet";
        private const string COMPUTED_HASH_FILENAME = "computed_hash.json";
        private const string TMP_COMPUTED_HASH_FILENAME = "tmp_computed_hash.json";

        private readonly Stopwatch _stopwatch;
        public HapetUpdateToolchain(Stopwatch stopwatch)
        {
            _stopwatch = stopwatch;
        }

        async public Task TryUpdateHapetAsync(IMessageHandler messageHandler)
        {
            using var webSevice = new WebService(messageHandler);
        }

        async private Task<bool> IsUpdateAvailableAsync(WebService webSevice)
        {
            string hapetPath = CompilerUtils.CurrentHapetDirectory;
            string hashFilePath = Path.Combine(hapetPath, TMP_COMPUTED_HASH_FILENAME);
            string existedHashFilePath = Path.Combine(hapetPath, COMPUTED_HASH_FILENAME);
            string platformFolderName = CompilerSettings.CurrentPlatformData.Name;

            // gettings hashes from server
            var result = await webSevice.ExecuteRequestTaskAsync(new FileRequest($"{HAPET_DOWNLOAD_LINK}/{platformFolderName}/{COMPUTED_HASH_FILENAME}", hashFilePath, null));
            if (result.IsExecutedNormally)
            {
                //try
                //{
                //    var hashes = JsonHelper.SmartParse<ComputedHashJson>(File.ReadAllText(hashFilePath));
                //    if (hashes == null)
                //    {
                //        // if there is no hash file on server - there is nothing to do then
                //        return false;
                //    }

                //    // remove tmp hashes
                //    if (File.Exists(hashFilePath))
                //        File.Delete(hashFilePath);

                //    // gettings existed hashes on update to compare them
                //    ComputedHashJson existedHashes = new ComputedHashJson();
                //    if (File.Exists(existedHashFilePath))
                //    {
                //        existedHashes = JsonHelper.SmartParse<ComputedHashJson>(File.ReadAllText(existedHashFilePath));
                //        return existedHashes.Version != hashes.Version;
                //    }
                //    return true;
                //}
                //catch (Exception ex)
                //{
                //    LoggingService.Error($"Error while check for update {product.Name}", ex);
                //}
            }
            return false;
        }
    }
}
