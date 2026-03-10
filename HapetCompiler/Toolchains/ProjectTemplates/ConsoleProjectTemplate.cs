using HapetFrontend.Entities;

namespace HapetCompiler.Toolchains.ProjectTemplates
{
    internal class ConsoleProjectTemplate : IProjectTemplate
    {
        public string DefaultProjectName => "ConsoleProject";
        public string TemplateDirectoryName => "ConsoleProjectTemplate";
        public string TemplateProjectFileName => "ConsoleProject.hptproj";
        public string TemplateDescription => "Console project";

        async public Task<bool> CreateAsync(string projectName, string[] args, IMessageHandler messageHandler)
        {
            // nothing to do here for now
            return await Task.FromResult(true);
        }
    }
}
