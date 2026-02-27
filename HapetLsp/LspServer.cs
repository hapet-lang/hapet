using HapetFrontend;
using HapetFrontend.ProjectParser;
using HapetLastPrepare;
using HapetLsp.Handlers;
using HapetPostPrepare;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace HapetLsp
{
    public class LspServer
    {
        public bool ShouldStop { get; set; } = false; // never changed anywhere. need for SonarCloud not to be angry
        async public Task StartAsync(ProjectXmlParser projectParser, Compiler compiler, PostPrepare pp, LastPrepare lp, Action projectResolver, bool useTcp = false)
        {
            if (useTcp)
            {
                var listener = new TcpListener(IPAddress.Loopback, 5007);
                listener.Start();
                while (!ShouldStop)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    var stream = client.GetStream();
                    await Task.Run(async () =>
                    {
                        var server = await CreateServer(stream, stream);
                        await server.WaitForExit;
                    });
                }
            }
            else
            {
                try
                {
                    var input = Console.OpenStandardInput();
                    var output = Console.OpenStandardOutput();

                    var server = await CreateServer(input, output);
                    await server.WaitForExit;
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync("Critical error in hapet lsp:");
                    await Console.Error.WriteLineAsync(ex.ToString());
                    await Console.Error.FlushAsync();
                    Environment.Exit(1);
                }
            }

            async Task<LanguageServer> CreateServer(Stream input, Stream output)
            {
                var server = await LanguageServer.From(options => {
                    options
                    .WithInput(input)
                    .WithOutput(output)
                    .WithServices(services =>
                    {
                        services.AddSingleton(projectParser);
                        services.AddSingleton(compiler);
                        services.AddSingleton(pp);
                        services.AddSingleton(lp);
                        services.AddSingleton(projectResolver);

                        services.AddTransient<HptprojSyncHandler>();
                        services.AddTransient<HptprojSemanticHandler>();
                        services.AddTransient<HapetSyncHandler>();
                        services.AddTransient<HapetSemanticHandler>();
                    })
                    .WithHandler<HptprojSyncHandler>()
                    .WithHandler<HptprojSemanticHandler>()
                    .WithHandler<HapetSyncHandler>()
                    .WithHandler<HapetSemanticHandler>()
                    ;
                });
                return server;
            }
        }
    }
}
