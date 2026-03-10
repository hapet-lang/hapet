using HapetCompiler.Toolchains.ProjectTemplates;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using System.Diagnostics;

namespace HapetCompiler.Toolchains
{
    internal sealed class ProjectNewToolchain
    {
        private static Dictionary<string, IProjectTemplate> _projectTemplates = new Dictionary<string, IProjectTemplate>()
        {
            { "console", new ConsoleProjectTemplate() },
        };

        private readonly Stopwatch _stopwatch;
        private readonly string[] _cmdArgs; // TODO: use them for ProjectXmlParser
        public ProjectNewToolchain(Stopwatch stopwatch, string[] args)
        {
            _stopwatch = stopwatch;
            _cmdArgs = args;
        }

        async public Task<int> CreateProjectAsync(string projectType, IMessageHandler messageHandler)
        {
            if (projectType == "--help")
            {
                // TODO: print help
                return await Task.FromResult(0);
            }

            // error if no such template
            if (!_projectTemplates.ContainsKey(projectType))
                return await Task.FromResult((int)CompilerErrors.HapetCommandError);

            // calling function for creation in template
            var template = _projectTemplates[projectType];

            // some shite with naming
            if (!GetProjectName(template, messageHandler, out string projectName))
                return await Task.FromResult((int)CompilerErrors.HapetCommandError);

            var result = await template.CreateAsync(projectName, _cmdArgs, messageHandler);
            if (result)
                messageHandler.ReportMessage([$"Project '{projectName}' successfully created"], null, ReportType.Info);
            return await Task.FromResult(0);
        }

        private bool GetProjectName(IProjectTemplate projectTemplate, IMessageHandler messageHandler, out string projectName)
        {
            // make default name for project - check for existance
            string defaultprojectName = projectTemplate.DefaultProjectName;
            string currentProjectName = defaultprojectName;
            int currentIteration = 0;
            string currentDirectory = Directory.GetCurrentDirectory();
            while (Directory.Exists(Path.Combine(currentDirectory, currentProjectName)))
            {
                currentIteration++;
                currentProjectName = defaultprojectName + currentIteration.ToString();
            }

            // check for -n param existance
            if (_cmdArgs.Contains("-n"))
            {
                int index = Array.FindIndex(_cmdArgs, x => x == "-n");
                if (index + 1 >= _cmdArgs.Length)
                {
                    // name not specified
                    messageHandler.ReportMessage(["Project name", "-n"], ErrorCode.Get(CTEN.SomethingExpectedAfter));
                    projectName = "";
                    return false;
                }
                currentProjectName = _cmdArgs[index + 1];
            }

            // check if directory already exists - then error
            if (Directory.Exists(Path.Combine(currentDirectory, currentProjectName)))
            {
                messageHandler.ReportMessage([currentProjectName], ErrorCode.Get(CTEN.TemplateProjectDirExists));
                projectName = "";
                return false;
            }

            projectName = currentProjectName;
            return true;
        }
    }
}
