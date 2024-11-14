using HapetCompiler.Toolchains;
using HapetFrontend.Helpers;

namespace HapetCompiler.Resolvers
{
	internal partial class ProjectReferencesResolver
	{
		private void ResolveProjectReferences()
		{
			string currentProjectPath = _projectSettings.ProjectPath;
			string currentProjectFolderPath = Path.GetDirectoryName(currentProjectPath).Replace("\\", "/").TrimEnd('/');
			foreach (var r in _projectData.ProjectReferences)
			{
				string projectPathNormalized = r.Replace("\\", "/").TrimStart('/');
				string pathToReferenced = $"{currentProjectFolderPath}/{projectPathNormalized}"; // TODO: error if doesn't exists
				// building the project
				var toolchain = new ProjectBuildToolchain(new string[0]); // TODO: params?
				toolchain.Build(pathToReferenced, _compiler.MessageHandler);

				string referencedProjectOutFolder = toolchain.ProjectSettings.OutputDirectory.Replace("\\", "/").TrimStart('/');
				string pathToReferencedFolder = Path.GetDirectoryName(pathToReferenced).Replace("\\", "/").TrimEnd('/');
				string fullReferencedOutFolder = $"{pathToReferencedFolder}/{referencedProjectOutFolder}"; // TODO: error if doesn't exists
				// copy all the files from the referenced project to current out folder
				Funcad.CopyFilesRecursively(fullReferencedOutFolder, _projectSettings.OutputDirectory);

				// adding the reference to dll
				_projectData.References.Add(toolchain.ProjectSettings.ProjectName);
			}
		}
	}
}
