using HapetFrontend.Entities;

namespace HapetCompiler.Toolchains.ProjectTemplates
{
    internal class ClassLibProjectTemplate : IProjectTemplate
    {
        public string DefaultProjectName => "LibraryProject";
        public string TemplateDirectoryName => "ClassLibProjectTemplate";
        public string TemplateProjectFileName => "ClassLibProject.hptproj";
        public string TemplateDescription => "Class library project";

        async public Task<bool> CreateAsync(string projectName, string[] args, IMessageHandler messageHandler)
        {
            // nothing to do here for now
            return await Task.FromResult(true);
        }
    }
}
