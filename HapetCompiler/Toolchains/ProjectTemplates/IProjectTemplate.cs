using HapetFrontend.Entities;

namespace HapetCompiler.Toolchains.ProjectTemplates
{
    internal interface IProjectTemplate
    {
        string DefaultProjectName { get; }
        Task CreateAsync(string projectName, string[] args, IMessageHandler messageHandler);
    }
}
