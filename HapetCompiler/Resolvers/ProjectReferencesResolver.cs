using HapetCompiler.ProjectConf.Data;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;

namespace HapetCompiler.Resolvers
{
    internal sealed partial class ProjectReferencesResolver
    {
        private ProjectData _projectData;
        private CompilerSettings _projectSettings;
        private Compiler _compiler;

        public void ResolveProjectShite(ProjectData projectData, CompilerSettings projectSettings, Compiler compiler)
        {
            _projectData = projectData;
            _projectSettings = projectSettings;
            _compiler = compiler;

            // TODO: package references

            if (!projectSettings.IsReferencedCompilation)
                compiler.MessageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(compiler.CompilationStopwatch.Elapsed)} Resolving project references..."], null, ReportType.Info);
            ResolveProjectReferences();

            if (compiler.MessageHandler.HasErrors)
                return; // references errors

            if (!projectSettings.IsReferencedCompilation)
                compiler.MessageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(compiler.CompilationStopwatch.Elapsed)} Resolving references..."], null, ReportType.Info);
            ResolveReferences();
        }
    }
}
