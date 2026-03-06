using HapetFrontend.Entities;

namespace HapetCompiler.Toolchains.ProjectTemplates
{
    internal class ConsoleProjectTemplate : IProjectTemplate
    {
        async public Task CreateAsync(string[] args, IMessageHandler messageHandler)
        {


            await Task.CompletedTask;
        }
    }
}
