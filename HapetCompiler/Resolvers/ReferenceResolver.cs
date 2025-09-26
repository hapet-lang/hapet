using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using LLVMSharp;
using Newtonsoft.Json;
using System.Reflection;
using HapetBackend.Llvm.Linkers;
using HapetFrontend.Errors;
using HapetPostPrepare;

namespace HapetCompiler.Resolvers
{
    internal partial class ProjectReferencesResolver
    {
        public List<string> PathsToLinkWith { get; } = new List<string>();
        public List<string> LibrariesToLinkWith { get; } = new List<string>();

        private void ResolveReferences()
        {
            string outFolder = _projectSettings.OutputDirectory;
            foreach (var r in _projectData.References)
            {
                if (!_projectSettings.IsReferencedCompilation && !_projectSettings.IsLspCompilation)
                    _compiler.MessageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_compiler.CompilationStopwatch.Elapsed)}   Resolving '{r}'..."], null, ReportType.Info);

                // getting the proper data
                bool isOk = LinkHelper.GetLibraryPaths(r, outFolder, out (string, string) data);
                if (!isOk)
                {
                    _compiler.MessageHandler.ReportMessage([r], ErrorCode.Get(CTEN.AssemblyNotFound));
                    continue;
                }

                var fullPathToTheFile = $"{data.Item1}/{data.Item2}.mpt";
                var metaText = File.ReadAllText(fullPathToTheFile).Replace("\r\n", "\n");
                var metadata = _compiler.HandleExternalMetadata(fullPathToTheFile, metaText);

                PathsToLinkWith.Add(data.Item1);
                // TODO: is there .lib file when we are on linux?
                LibrariesToLinkWith.Add($"{data.Item2}.lib");

                // pure project name that has to be contained inside .mpt file
                _projectData.AllReferencedProjectNames.Add(metadata.Name);
            }
        }
    }
}
