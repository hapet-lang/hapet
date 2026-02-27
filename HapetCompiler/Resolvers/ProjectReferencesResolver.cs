using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend.ProjectParser;

namespace HapetCompiler.Resolvers
{
    internal sealed partial class ProjectReferencesResolver
    {
        private ProjectData _projectData;
        private CompilerSettings _projectSettings;
        private Compiler _compiler;
        private ProjectXmlParser _projectXmlParser;

        public void ResolveProjectShite(ProjectData projectData, CompilerSettings projectSettings, Compiler compiler, ProjectXmlParser projectXmlParser)
        {
            _projectData = projectData;
            _projectSettings = projectSettings;
            _compiler = compiler;
            _projectXmlParser = projectXmlParser;

            // TODO: package references

            if (!projectSettings.IsReferencedCompilation && !projectSettings.IsLspCompilation && !CompilerSettings.IsInRunContext)
                compiler.MessageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(compiler.CompilationStopwatch.Elapsed)} Resolving project references..."], null, ReportType.Info);
            ResolveProjectReferences();

            // do allow go further when lsp
            if (compiler.MessageHandler.HasErrors && !projectSettings.IsLspCompilation)
                return; // references errors

#if DEBUG
            if (!projectSettings.IsReferencedCompilation && !projectSettings.IsLspCompilation && !CompilerSettings.IsInRunContext)
                compiler.MessageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(compiler.CompilationStopwatch.Elapsed)} Resolving references..."], null, ReportType.Info);
#endif
            ResolveReferences();
        }
    }
}
