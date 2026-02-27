using HapetBackend.Llvm.Linkers;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetPostPrepare;
using LLVMSharp;
using Newtonsoft.Json;
using System.Reflection;

namespace HapetCompiler.Resolvers
{
    internal partial class ProjectReferencesResolver
    {
        public List<string> PathsToLinkWith { get; } = new List<string>();
        public List<string> LibrariesToLinkWith { get; } = new List<string>();

        private void ResolveReferences()
        {
            _projectData.AllReferencedProjectNames.Clear();
            LibrariesToLinkWith.Clear();
            PathsToLinkWith.Clear();

            string outFolder = _projectSettings.OutputDirectory;
            foreach (var r in _projectData.References)
            {
#if DEBUG
                if (!_projectSettings.IsReferencedCompilation && !_projectSettings.IsLspCompilation && !CompilerSettings.IsInRunContext)
                    _compiler.MessageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_compiler.CompilationStopwatch.Elapsed)}   Resolving '{r}'..."], null, ReportType.Info);
#endif
                // getting the proper data
                bool isOk = LinkHelper.GetLibraryPaths(r.ReferenceName, outFolder, out (string, string) data);
                if (!isOk)
                {
                    var loc = _projectXmlParser.XmlProgramFile.GetLocationFromSpan(r.Node.AsElement.Start, r.Node.AsElement.Start + r.Node.AsElement.FullWidth);
                    _compiler.MessageHandler.ReportMessage(_projectXmlParser.XmlProgramFile, loc, [r.ReferenceName], ErrorCode.Get(CTEN.AssemblyNotFound));
                    continue;
                }

                var fullPathToTheFile = $"{data.Item1}/{data.Item2}.mpt";
                var metaText = File.ReadAllText(fullPathToTheFile);
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
