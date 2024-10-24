using HapetCommon;
using HapetFrontend.Entities;
using System.Xml;

namespace HapetCompiler
{
	internal class ProjectXmlParser
	{
		public ProjectXmlParser(string projectPath, IErrorHandler errorHandler)
		{
			XmlDocument projDoc = new XmlDocument();
			projDoc.Load(projectPath);
			if (projDoc == null)
			{
				errorHandler.ReportError($"Project file {projectPath} could not be parsed");
				return;
			}

			// hptproj should be parsed here
			CompilerSettings.TargetPlatformData = CompilerSettings.SupportedPlatforms.FirstOrDefault(x => x.TargetPlatform == TargetPlatform.Win86);
			CompilerSettings.TargetFormat = TargetFormat.Console;
		}
	}
}
