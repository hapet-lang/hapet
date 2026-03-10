using HapetFrontend.Entities;

namespace HapetCompiler.Toolchains.ProjectTemplates
{
    internal interface IProjectTemplate
    {
        string DefaultProjectName { get; }
        string TemplateDirectoryName { get; }
        string TemplateProjectFileName { get; }
        string TemplateDescription { get; }
        Task<bool> CreateAsync(string projectName, string[] args, IMessageHandler messageHandler);
    }
}
