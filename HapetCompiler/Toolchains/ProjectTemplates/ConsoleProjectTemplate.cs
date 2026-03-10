using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using System.Linq;

namespace HapetCompiler.Toolchains.ProjectTemplates
{
    internal class ConsoleProjectTemplate : IProjectTemplate
    {
        public string DefaultProjectName => "ConsoleProject";

        async public Task<bool> CreateAsync(string projectName, string[] args, IMessageHandler messageHandler)
        {
            // dir where hapet is running
            string currentDirectory = Directory.GetCurrentDirectory();

            // create dir for the project
            string projectDirectory = Path.Combine(currentDirectory, projectName);
            Directory.CreateDirectory(projectDirectory);

            // copy template files
            string consoleTemplate = Path.Combine(CompilerUtils.CurrentHapetDirectory, "ProjectTemplateData", "ConsoleProjectTemplate");
            CompilerUtils.CopyFilesRecursively(consoleTemplate, projectDirectory);

            // change project name
            string projectFileName = Path.Combine(projectDirectory, "ConsoleProject.hptproj");
            string projectText = File.ReadAllText(projectFileName);
            File.Delete(projectFileName);
            projectText = projectText.Replace("{ProjectName}", projectName);
            // save as new file
            string newProjectFileName = Path.Combine(projectDirectory, $"{projectName}.hptproj");
            File.WriteAllText(newProjectFileName, projectText);

            return await Task.FromResult(true);
        }
    }
}
