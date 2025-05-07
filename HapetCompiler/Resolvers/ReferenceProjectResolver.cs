using HapetCompiler.Toolchains;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;

namespace HapetCompiler.Resolvers
{
    internal partial class ProjectReferencesResolver
    {
        private void ResolveProjectReferences()
        {
            string currentProjectPath = _projectSettings.ProjectPath;
            string currentProjectFolderPath = Path.GetDirectoryName(currentProjectPath).Replace("\\", "/", StringComparison.InvariantCulture).TrimEnd('/');
            foreach (var r in _projectData.ProjectReferences)
            {
                if (!_projectSettings.IsReferencedCompilation)
                    _compiler.MessageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_compiler.CompilationStopwatch.Elapsed)}   Resolving '{Path.GetFileName(r)}'..."], null, ReportType.Info);

                string projectPathNormalized = r.Replace("\\", "/", StringComparison.InvariantCulture).TrimStart('/');
                string pathToReferenced = $"{currentProjectFolderPath}/{projectPathNormalized}"; 
                // error if doesn't exists
                if (!File.Exists(pathToReferenced))
                    _compiler.MessageHandler.ReportMessage([projectPathNormalized], ErrorCode.Get(CTEN.RefProjectNotFound));

                // building the project
                var toolchain = new ProjectBuildToolchain([]); // TODO: params?
                toolchain.Build(pathToReferenced, _compiler.MessageHandler, true);

                string referencedProjectOutFolder = toolchain.ProjectSettings.OutputDirectory.Replace("\\", "/", StringComparison.InvariantCulture).TrimStart('/');
                // copy all the files from the referenced project to current out folder
                Funcad.CopyFilesRecursively(referencedProjectOutFolder, _projectSettings.OutputDirectory);

                // adding the reference to dll
                _projectData.References.Add(toolchain.ProjectSettings.ProjectName);
            }
        }
    }
}
