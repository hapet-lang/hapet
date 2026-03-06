using HapetCompiler.Toolchains.ProjectTemplates;
using HapetFrontend;
using HapetFrontend.Entities;
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
            {
                return await Task.FromResult((int)CompilerErrors.HapetCommandError);
            }

            // calling function for creation in template
            var template = _projectTemplates[projectType];
            await template.CreateAsync(_cmdArgs, messageHandler);
            return await Task.FromResult(0);
        }
    }
}
