using HapetFrontend.Entities;
using HapetFrontend.Errors;
using System.Linq;

namespace HapetCompiler.Toolchains.ProjectTemplates
{
    internal class ConsoleProjectTemplate : IProjectTemplate
    {
        public string DefaultProjectName => "ConsoleProject";

        async public Task CreateAsync(string projectName, string[] args, IMessageHandler messageHandler)
        {
            // dir where hapet is running
            string currentDirectory = Directory.GetCurrentDirectory();

            // create dir for the project
            Directory.CreateDirectory(Path.Combine(currentDirectory, projectName));


            await Task.CompletedTask;
        }
    }
}
