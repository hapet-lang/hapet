using HapetFrontend;
using HapetFrontend.ProjectParser;
using HapetLastPrepare;
using HapetLsp.Handlers;
using HapetPostPrepare;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Server;
using System.Net;
using System.Net.Sockets;

namespace HapetLsp
{
    public class LspServer
    {
        public bool ShouldStop { get; set; } = false; // never changed anywhere. need for SonarCloud not to be angry
        async public Task StartAsync(ProjectXmlParser projectParser, Compiler compiler, PostPrepare pp, LastPrepare lp)
        {
            var listener = new TcpListener(IPAddress.Loopback, 5007);
            listener.Start();
            while (!ShouldStop)
            {
                var client = await listener.AcceptTcpClientAsync();
                var stream = client.GetStream();
                await Task.Run(async () =>
                {
                    var server = await LanguageServer.From(options => {
                        options
                        .WithInput(stream)
                        .WithOutput(stream)
                        .WithServices(services =>
                        {
                            services.AddSingleton(projectParser);
                            services.AddSingleton(compiler);
                            services.AddSingleton(pp);
                            services.AddSingleton(lp);

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
                    await server.WaitForExit;
                });
            }
        }
    }
}
