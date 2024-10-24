using HapetCommon;
using HapetFrontend;
using HapetFrontend.Entities;
using System.Xml;

namespace HapetCompiler
{
	internal class ProjectXmlParser
	{
		private Dictionary<string, string> _propertyGroupData = new Dictionary<string, string>();

		public ProjectXmlParser(string projectPath, IErrorHandler errorHandler)
		{
			XmlDocument projDoc = new XmlDocument();
			projDoc.Load(projectPath);
			if (projDoc == null)
			{
				errorHandler.ReportError($"Project file {projectPath} could not be parsed");
				return;
			}

			XmlElement projRoot = projDoc.DocumentElement;
			foreach (XmlElement xnode in projRoot)
			{
				// check that the tag is PropertyGroup
				if (xnode.Name == "PropertyGroup")
				{
					// go all over the project settings
					foreach (XmlNode childnode in xnode.ChildNodes)
					{
						_propertyGroupData.Add(childnode.Name, childnode.FirstChild.Value);
					}
				}
			}

			// hptproj should be parsed here
			CompilerSettings.TargetPlatformData = CompilerSettings.SupportedPlatforms.FirstOrDefault(x => x.TargetPlatform == TargetPlatform.Win86);
			CompilerSettings.TargetFormat = TargetFormat.Console;
		}

		public void UpdateSettings()
		{
			// setting target format
			CompilerSettings.TargetFormat = GetValueOrDefault<TargetFormat>("TargetFormat", TargetFormat.Console);
			// setting platform data
			string targetPlatform = GetValueOrDefault<string>("TargetPlatform", "");
			if (string.IsNullOrEmpty(targetPlatform)) CompilerSettings.TargetPlatformData = CompilerSettings.CurrentPlatformData;
			else CompilerSettings.TargetPlatformData = CompilerSettings.SupportedPlatforms.FirstOrDefault(x => x.Name == targetPlatform);

			// TODO:
		}

		private T GetValueOrDefault<T>(string key, T defaultValue)
		{
			if (_propertyGroupData.TryGetValue(key, out var value))
			{
				if (typeof(bool) == typeof(T))
				{

				}
			}
			else
			{
				return defaultValue;
			}
		}
	}
}
