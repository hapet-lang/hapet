using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend.ProjectParser;

namespace HapetCompiler.Resolvers
{
    internal sealed partial class ProjectReferencesResolver
    {
        private ProjectData _projectData;
        private Compiler _compiler;
        private ProjectXmlParser _projectXmlParser;

        public void ResolveProjectShite(ProjectData projectData, Compiler compiler, ProjectXmlParser projectXmlParser)
        {
            _projectData = projectData;
            _compiler = compiler;
            _projectXmlParser = projectXmlParser;

            // TODO: package references

            if (!projectData.IsReferencedCompilation && !CompilerSettings.IsInLspContext && !CompilerSettings.IsInRunContext)
                compiler.MessageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(compiler.CompilationStopwatch.Elapsed)} Resolving project references..."], null, ReportType.Info);
            ResolveProjectReferences();

            // do allow go further when lsp
            if (compiler.MessageHandler.HasErrors && !CompilerSettings.IsInLspContext)
                return; // references errors

#if DEBUG
            if (!projectData.IsReferencedCompilation && !CompilerSettings.IsInLspContext && !CompilerSettings.IsInRunContext)
                compiler.MessageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(compiler.CompilationStopwatch.Elapsed)} Resolving references..."], null, ReportType.Info);
#endif
            ResolveReferences();
        }
    }
}
