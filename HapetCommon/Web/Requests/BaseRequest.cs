using HapetCommon.Messaging;

namespace HapetCommon.Web.Requests
{
    public abstract class BaseRequest
    {
        protected volatile object _result;

        public BaseRequest()
        {
        }

        public abstract Task<RequestResult> Execute(ICommonMessageHandler messageHandler, HttpClient httpClient);
    }
}
