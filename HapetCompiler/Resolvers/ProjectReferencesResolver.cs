using HapetCompiler.ProjectConf.Data;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing.PostPrepare;
using System.Diagnostics;

namespace HapetCompiler.Resolvers
{
    internal partial class ProjectReferencesResolver
    {
        private ProjectData _projectData;
        private CompilerSettings _projectSettings;
        private Compiler _compiler;
        private PostPrepare _postPreparer;

        public void ResolveProjectShite(ProjectData projectData, CompilerSettings projectSettings, Compiler compiler, PostPrepare postPreparer)
        {
            _projectData = projectData;
            _projectSettings = projectSettings;
            _compiler = compiler;
            _postPreparer = postPreparer;

            // TODO: project references

            if (!projectSettings.IsReferencedCompilation)
                compiler.MessageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(compiler.CompilationStopwatch.Elapsed)} Resolving project references...", ReportType.Info);
            ResolveProjectReferences();
            if (!projectSettings.IsReferencedCompilation)
                compiler.MessageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(compiler.CompilationStopwatch.Elapsed)} Resolving references...", ReportType.Info);
            ResolveReferences();
        }
    }
}
