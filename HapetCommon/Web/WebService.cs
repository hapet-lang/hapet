using HapetCommon.Messaging;
using HapetCommon.Web.Requests;
using System.Net;
using System.Net.Sockets;

namespace HapetCommon.Web
{
    public class WebService
    {
        private readonly HttpClient _httpClient;
        private readonly ICommonMessageHandler _messageHandler;

        public WebService(ICommonMessageHandler messageHandler)
        {
            var handler = GetHttpHandler(true);
            _httpClient = new HttpClient(handler);
            _messageHandler = messageHandler;
        }

        public async Task<RequestResult> ExecuteRequestTaskAsync(BaseRequest request, bool force = false)
        {
            bool connection = await CheckInternetConnection();

            // return null if there is no connection and the request is not forced to be executed
            if (!connection && !force)
            {
                return new RequestResult(false, null, HttpStatusCode.GatewayTimeout);
            }
            RequestResult result = await request.Execute(_messageHandler, _httpClient);

            if (!result.IsExecutedNormally) { await CheckInternetConnection(); }

            return result;
        }

        public async Task<bool> CheckInternetConnection()
        {
            return (await (new CheckConnectionRequest()).Execute(_messageHandler, _httpClient)).IsExecutedNormally;
        }

        #region Http handler shite
        private static HttpMessageHandler GetHttpHandler(bool sctHandler)
        {
            if (sctHandler)
            {
                return new SocketsHttpHandler()
                {
                    ConnectCallback = async (context, cancellationToken) =>
                    {
                        // Use DNS to look up the IP address(es) of the target host
                        IPHostEntry ipHostEntry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host);

                        // Filter for IPv4 addresses only
                        IPAddress ipAddress = ipHostEntry
                            .AddressList
                            .FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork);

                        // Fail the connection if there aren't any IPV4 addresses
                        if (ipAddress == null)
                        {
                            throw new Exception($"No IP4 address for {context.DnsEndPoint.Host}");
                        }

                        // Open the connection to the target host/port
                        TcpClient tcp = new();
                        await tcp.ConnectAsync(ipAddress, context.DnsEndPoint.Port, cancellationToken);

                        // Return the NetworkStream to the caller
                        return tcp.GetStream();
                    },
                    Proxy = null,
                    UseProxy = false,
                    SslOptions = new System.Net.Security.SslClientAuthenticationOptions()
                    {
                        CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck,
                        EnabledSslProtocols = System.Security.Authentication.SslProtocols.None,
                        RemoteCertificateValidationCallback = (s, c, ch, ssl) =>
                        {
                            return true;
                        }
                    }
                };
            }
            else
            {
                return new HttpClientHandler()
                {
                    ClientCertificateOptions = ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback =
                        (httpRequestMessage, cert, cetChain, policyErrors) =>
                        {
                            return true;
                        },
                    MaxConnectionsPerServer = 10,
                    Proxy = null,
                    UseProxy = false,
                };
            }
        }
        #endregion
    }
}
