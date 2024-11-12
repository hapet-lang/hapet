using HapetCompiler.ProjectConf.Data;
using HapetFrontend;
using HapetFrontend.Parsing.PostPrepare;

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

			ResolveReferences();
		}
	}
}
