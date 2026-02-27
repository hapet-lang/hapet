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
            string currentProjectPath = _projectSettings.ProjectPath;
            string currentProjectFolderPath = Path.GetDirectoryName(currentProjectPath).Replace("\\", "/", StringComparison.InvariantCulture).TrimEnd('/');
            foreach (var r in _projectData.ProjectReferences)
            {
                if (!_projectSettings.IsReferencedCompilation && !_projectSettings.IsLspCompilation && !CompilerSettings.IsInRunContext)
                    _compiler.MessageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_compiler.CompilationStopwatch.Elapsed)}   Resolving '{Path.GetFileName(r.ReferenceName)}'..."], null, ReportType.Info);

                string projectPathNormalized = r.ReferenceName.Replace("\\", "/", StringComparison.InvariantCulture).TrimStart('/');
                string pathToReferenced = $"{currentProjectFolderPath}/{projectPathNormalized}"; 
                // error if doesn't exists
                if (!File.Exists(pathToReferenced))
                {
                    var loc = _projectXmlParser.XmlProgramFile.GetLocationFromSpan(r.Node.AsElement.Start, r.Node.AsElement.Start + r.Node.AsElement.FullWidth);
                    _compiler.MessageHandler.ReportMessage(_projectXmlParser.XmlProgramFile, loc, [projectPathNormalized], ErrorCode.Get(CTEN.RefProjectNotFound));
                    continue;
                }

                // building the project
                var toolchain = new ProjectBuildToolchain(_compiler.CompilationStopwatch, []); // TODO: params?
                // make codegen when not lsp
                bool codegenRequired = !_projectSettings.IsLspCompilation;
                toolchain.Build(pathToReferenced, _compiler.MessageHandler, out string _, true, codegenRequired);

                string referencedProjectOutFolder = toolchain.ProjectSettings.OutputDirectory.Replace("\\", "/", StringComparison.InvariantCulture).TrimStart('/');
                // copy all the files from the referenced project to current out folder
                Funcad.CopyFilesRecursively(referencedProjectOutFolder, _projectSettings.OutputDirectory);

                // adding the reference to dll
                _projectData.References.Add(new Reference(toolchain.ProjectSettings.AssemblyName, r.Node));
            }
        }
    }
}
