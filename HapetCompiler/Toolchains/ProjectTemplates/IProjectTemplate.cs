using HapetFrontend.Entities;

namespace HapetCompiler.Toolchains.ProjectTemplates
{
    internal interface IProjectTemplate
    {
        Task CreateAsync(string[] args, IMessageHandler messageHandler);
    }
}
