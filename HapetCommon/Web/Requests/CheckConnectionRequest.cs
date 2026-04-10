using HapetCommon.Messaging;

namespace HapetCommon.Web.Requests
{
    public class CheckConnectionRequest : BaseRequest
    {
        public static string CheckConnectionUrl { get; private set; } = "https://hapetlang.com";

        public override async Task<RequestResult> Execute(ICommonMessageHandler messageHandler, HttpClient httpClient)
        {
            _result = false;
            try
            {
                int currTry = 0;
                while (currTry < 3)
                {
                    using var resp = await httpClient.GetAsync(CheckConnectionUrl);
                    _result = resp.StatusCode == System.Net.HttpStatusCode.OK;
                    if ((bool)_result) break;
                    currTry++;
                    messageHandler.ReportMessage($"No internet connection. Retrying in 3 seconds...");
                    await Task.Delay(3000);
                }
            }
            catch (Exception ex)
            {
                messageHandler.ReportMessage($"Unexpected exception occured while checking internet connection. Error log: {ex}");
            }
            return new RequestResult((bool)_result, _result, System.Net.HttpStatusCode.OK);
        }
    }
}
