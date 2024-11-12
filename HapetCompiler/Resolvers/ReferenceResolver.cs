using HapetFrontend.Parsing.PostPrepare;
using LLVMSharp;
using Newtonsoft.Json;

namespace HapetCompiler.Resolvers
{
	internal partial class ProjectReferencesResolver
	{
		private void ResolveReferences()
		{
			foreach (var r in _projectData.References)
			{
				string fileName = $"{r}.json";
				string outFolder = _projectSettings.OutputDirectory;
				string theAssemblyPath;

				// mb some other checks?
				if (File.Exists(fileName))
					theAssemblyPath = fileName;
				else if (File.Exists($"{outFolder}/{fileName}"))
					theAssemblyPath = $"{outFolder}/{fileName}";
				else
				{
					_compiler.MessageHandler.ReportMessage($"File {fileName} could not be found. Please check project references properly");
					continue;
				}

				var jsonText = File.ReadAllText(theAssemblyPath);
				var metadata = JsonConvert.DeserializeObject<MetadataJson>(jsonText);
				_postPreparer.PostPrepareExternalMetadata(metadata, fileName);
			}
		}
	}
}
