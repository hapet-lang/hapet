using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HapetLsp
{
    public class LspServer
    {
        async public Task StartAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 5007);
            listener.Start();
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                var stream = client.GetStream();
                // предположим, ты можешь передать stream как input и output в серверные опции
                await Task.Run(async () =>
                {
                    var server = await LanguageServer.From(options => {
                        options
                        .WithInput(stream)
                        .WithOutput(stream)
                        .WithServices(services =>
                        {
                            services.AddTransient<HptprojSyncHandler>();
                        })
                        .WithHandler<HptprojSyncHandler>()
                        ;
                        // регистрация
                    });
                    await server.WaitForExit;
                });
            }
        }
    }
}
