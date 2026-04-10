namespace HapetCommon.Extensions
{
    public static class HttpClientExtensions
    {
        public static async Task DownloadAsync(this HttpClient client, string requestUri, Stream destination, Action<float> progressChanged = null, Action<ulong> downloadSpeed = null, CancellationToken cancellationToken = default)
        {
            // Get the http headers first to examine the content length
            using (var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead))
            {
                var contentLength = response.Content.Headers.ContentLength;

                using (var download = await response.Content.ReadAsStreamAsync(cancellationToken))
                {
                    // Ignore progress reporting when no progress reporter was 
                    // passed or when the content length is unknown
                    if (!contentLength.HasValue)
                    {
                        await download.CopyToAsync(destination, cancellationToken);
                        return;
                    }

                    // Convert absolute progress (bytes downloaded) into relative progress (0% - 100%)
                    // Use extension method to report progress while downloading
                    await download.CopyToAsync(destination, 81920, contentLength.Value, progressChanged, downloadSpeed, cancellationToken);
                    progressChanged?.Invoke(1);
                }
            }
        }
    }
}
