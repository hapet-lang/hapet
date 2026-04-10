using System.Net;

namespace HapetCommon.Web.Requests
{
    public struct RequestResult
    {
        public bool IsExecutedNormally { get; set; }
        public object Result { get; set; }
        public HttpStatusCode StatusCode { get; set; }

        public RequestResult(bool isExecutedNormally, object result, HttpStatusCode statusCode)
        {
            IsExecutedNormally = isExecutedNormally;
            Result = result;
            StatusCode = statusCode;
        }
    }
}
