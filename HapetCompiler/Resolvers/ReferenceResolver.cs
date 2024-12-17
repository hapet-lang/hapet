using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend;
using HapetFrontend.Parsing.PostPrepare;
using LLVMSharp;
using Newtonsoft.Json;
using System.Reflection;
using HapetBackend.Llvm.Linkers;

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
                if (!_projectSettings.IsReferencedCompilation)
                    _compiler.MessageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(_compiler.CompilationStopwatch.Elapsed)}   Resolving '{r}'...", ReportType.Info);

                // getting the proper data
                bool isOk = LinkHelper.GetLibraryPaths(r, outFolder, out (string, string) data);
                if (!isOk)
                {
                    _compiler.MessageHandler.ReportMessage($"Assembly {r} could not be found. Please check project references properly");
                    continue;
                }

                var fullPathToTheFile = $"{data.Item1}/{data.Item2}.json";
                var jsonText = File.ReadAllText(fullPathToTheFile);
                var metadata = JsonConvert.DeserializeObject<MetadataJson>(jsonText);
                _postPreparer.PostPrepareExternalMetadata(metadata, r);

                PathsToLinkWith.Add(data.Item1);
                // TODO: is there .lib file when we are on linux?
                LibrariesToLinkWith.Add($"{data.Item2}.lib");
            }
        }
    }
}
