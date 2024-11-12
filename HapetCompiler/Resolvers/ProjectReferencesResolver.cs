using HapetCompiler.ProjectConf.Data;
using HapetFrontend;

namespace HapetCompiler.Resolvers
{
	internal partial class ProjectReferencesResolver
	{
		private ProjectData _projectData;
		private Compiler _compiler;

		public void ResolveProjectShite(ProjectData projectData, Compiler compiler)
		{
			_projectData = projectData;
			_compiler = compiler;

			// TODO: project references

			// ResolveReferences();
		}
	}
}
