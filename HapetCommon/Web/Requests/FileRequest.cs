using HapetCommon.Extensions;
using HapetCommon.Messaging;

namespace HapetCommon.Web.Requests
{
    // https://stackoverflow.com/questions/20661652/progress-bar-with-httpclient
    public class FileRequest : BaseRequest
    {
        private readonly string _url;
        private readonly string _outPath;
        private readonly Action<float> _progressChanged;
        private readonly Action<ulong> _downloadSpeed;
        private readonly CancellationToken _cancellationToken;

        public FileRequest(string url, string outPath, Action<float> progressChanged = null, Action<ulong> downloadSpeed = null, CancellationToken cancellationToken = default)
        {
            _url = url;
            _outPath = outPath;
            _progressChanged = progressChanged;
            _downloadSpeed = downloadSpeed;
            _cancellationToken = cancellationToken;
        }

        public override async Task<RequestResult> Execute(ICommonMessageHandler messageHandler, HttpClient httpClient)
        {
            try
            {
                // Create a file stream to store the downloaded data.
                // This really can be any type of writeable stream.
                using (var file = new FileStream(_outPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // Use the custom extension method below to download the data.
                    // The passed progress-instance will receive the download status updates.
                    await httpClient.DownloadAsync(_url, file, _progressChanged, _downloadSpeed, _cancellationToken);
                }

                return new RequestResult(true, null, System.Net.HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                messageHandler.ReportMessage($"Exception while downloading file from uri: {_url}, the exception: {ex}");
            }
            return new RequestResult(false, null, System.Net.HttpStatusCode.NotFound);
        }
    }
}
