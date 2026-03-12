using HapetCompiler.Toolchains;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using System.Xml;

namespace HapetCompiler.Resolvers
{
    internal partial class ProjectReferencesResolver
    {
        private void ResolveProjectReferences()
        {
            string currentProjectFolderPath = Path.GetDirectoryName(_projectData.ProjectPath).Replace("\\", "/", StringComparison.InvariantCulture).TrimEnd('/');
            string currentStdDirectoryPath = Path.Combine(CompilerUtils.CurrentHapetDirectory, "std").Replace("\\", "/", StringComparison.InvariantCulture).TrimEnd('/');

            foreach (var r in _projectData.ProjectReferences)
            {
                if (!_projectData.IsReferencedCompilation && !CompilerSettings.IsInLspContext && !CompilerSettings.IsInRunContext)
                    _compiler.MessageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_compiler.CompilationStopwatch.Elapsed)}   Resolving '{Path.GetFileName(r.ReferenceName)}'..."], null, ReportType.Info);

                string projectPathNormalized = r.ReferenceName.Replace("\\", "/", StringComparison.InvariantCulture).TrimStart('/');
                string pathToReferenced = $"{currentProjectFolderPath}/{projectPathNormalized}"; 
                // error if doesn't exists
                if (!File.Exists(pathToReferenced))
                {
                    // if path to project not relative to current project then check it in STD folder
                    pathToReferenced = $"{currentStdDirectoryPath}/{Path.GetFileNameWithoutExtension(projectPathNormalized)}/{projectPathNormalized}";
                    if (!File.Exists(pathToReferenced))
                    {
                        var loc = _projectXmlParser.XmlProgramFile.GetLocationFromSpan(r.Node.AsElement.Start, r.Node.AsElement.Start + r.Node.AsElement.FullWidth);
                        _compiler.MessageHandler.ReportMessage(_projectXmlParser.XmlProgramFile, loc, [projectPathNormalized], ErrorCode.Get(CTEN.RefProjectNotFound));
                        continue;
                    }
                }

                // building the project
                var toolchain = new ProjectBuildToolchain(_compiler.CompilationStopwatch, []); // TODO: params?
                // make codegen when not lsp
                bool codegenRequired = !CompilerSettings.IsInLspContext;
                toolchain.Build(pathToReferenced, _compiler.MessageHandler, out string _, true, codegenRequired);

                string referencedProjectOutFolder = toolchain.ProjectData.OutputDirectory.Replace("\\", "/", StringComparison.InvariantCulture).TrimStart('/');
                // copy all the files from the referenced project to current out folder
                Funcad.CopyFilesRecursively(referencedProjectOutFolder, _projectData.OutputDirectory);

                // adding the reference to dll
                _projectData.References.Add(new Reference(toolchain.ProjectData.AssemblyName, r.Node));
            }
        }
    }
}
