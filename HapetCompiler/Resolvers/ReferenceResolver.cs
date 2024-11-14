using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend;
using HapetFrontend.Parsing.PostPrepare;
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
			foreach (var r in _projectData.References)
			{
				string fileName = $"{r}.json";
				string outFolder = _projectSettings.OutputDirectory;
				string theAssemblyPath;
				string pathToLink;

				_compiler.MessageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(_compiler.CompilationStopwatch.Elapsed)}   Resolving '{r}'...", ReportType.Info);

				// mb some other checks?
				if (File.Exists(fileName))
				{
					pathToLink = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
					theAssemblyPath = fileName;
				}
				else if (File.Exists($"{outFolder}/{fileName}"))
				{
					pathToLink = outFolder;
					theAssemblyPath = $"{outFolder}/{fileName}";
				}
				else
				{
					_compiler.MessageHandler.ReportMessage($"File {fileName} could not be found. Please check project references properly");
					continue;
				}

				var jsonText = File.ReadAllText(theAssemblyPath);
				var metadata = JsonConvert.DeserializeObject<MetadataJson>(jsonText);
                _postPreparer.PostPrepareExternalMetadata(metadata, r);

				PathsToLinkWith.Add(pathToLink);
				// TODO: is there .lib file when we are on linux?
				string fullPathToLib = $"{pathToLink}/{r}.lib";
				if (!File.Exists(fullPathToLib))
				{
					_compiler.MessageHandler.ReportMessage($"File {fullPathToLib} could not be found. Please check project references properly");
				}
				LibrariesToLinkWith.Add($"{r}.lib");
			}
		}
	}
}
