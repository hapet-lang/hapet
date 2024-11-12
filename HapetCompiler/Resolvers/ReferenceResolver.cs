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
				if (!File.Exists(fileName))
				{
					_compiler.MessageHandler.ReportMessage($"File {fileName} could not be found. Please check project references properly");
					continue;
				}
				var metadata = JsonConvert.DeserializeObject<MetadataJson>(fileName);
				var postPreparer = new PostPrepare(_compiler);
				postPreparer.PostPrepareExternalMetadata(metadata);
			}
		}
	}
}
