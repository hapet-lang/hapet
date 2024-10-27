using HapetFrontend;
using HapetFrontend.Entities;
using System.Xml;

namespace HapetCompiler
{
	internal class ProjectXmlParser
	{
		private readonly string _projectPath = string.Empty;
		private readonly string _projectPathAbsolute = string.Empty;
		private readonly CompilerSettings _projectSettings = null;
		private readonly IErrorHandler _errorHandler = null;

		private Dictionary<string, string> _propertyGroupData = new Dictionary<string, string>();

		// TODO: this class also should get args from cmd that would have bigger priority over defined in .hptproj file
		public ProjectXmlParser(string projectPath, CompilerSettings projectSettings, IErrorHandler errorHandler)
		{
			_projectPath = projectPath;
			_projectPathAbsolute = Path.GetFullPath(_projectPath);
			_projectSettings = projectSettings;
			_errorHandler = errorHandler;

			XmlDocument projDoc = new XmlDocument();
			projDoc.Load(_projectPath);
			if (projDoc == null)
			{
				_errorHandler.ReportError($"Project file {_projectPath} could not be parsed");
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
				// TODO: ...
			}

			// setting the project path into the settings
            _projectSettings.ProjectPath = _projectPath;
        }

		public void UpdateSettings()
		{
			// setting project name
			string projectFileName = Path.GetFileNameWithoutExtension(_projectPath);
			_projectSettings.ProjectName = GetValueOrDefault<string>("ProjectName", projectFileName);
			// setting project version
			_projectSettings.ProjectVersion = GetValueOrDefault<string>("ProjectVersion", "1.0.0");
			// setting project configuration
			_projectSettings.ProjectConfiguration = GetValueOrDefault<string>("ProjectConfiguration", "Debug");
			// setting project out folder
			var outDirRelative = GetValueOrDefault<string>("OutputDirectory", $"./bin/{_projectSettings.ProjectConfiguration}");
			_projectSettings.OutputDirectory = $"{Path.GetDirectoryName(_projectPathAbsolute).Replace("\\", "/").TrimEnd('/')}/{outDirRelative.Replace("\\", "/")}";
			// WARN: creating the dir here!!!
			if (!Directory.Exists(_projectSettings.OutputDirectory)) Directory.CreateDirectory(_projectSettings.OutputDirectory);

			// setting unsafe code allowence
			_projectSettings.AllowUnsafeCode = GetValueOrDefault<bool>("AllowUnsafeCode", false);

			// setting target format
			_projectSettings.TargetFormat = GetValueOrDefault<TargetFormat>("TargetFormat", TargetFormat.Console);
			// setting platform data
			string targetPlatform = GetValueOrDefault<string>("TargetPlatform", "");
			if (string.IsNullOrEmpty(targetPlatform)) _projectSettings.TargetPlatformData = CompilerSettings.CurrentPlatformData;
			else _projectSettings.TargetPlatformData = CompilerSettings.SupportedPlatforms.FirstOrDefault(x => x.Name == targetPlatform);

			// TODO:
		}

		// TODO: add allowed items parameter
		// for example when parsing ProjectConfiguration should be checked
		private T GetValueOrDefault<T>(string key, T defaultValue)
		{
			try
			{
				if (_propertyGroupData.TryGetValue(key, out var value))
				{
					// TODO: better casts. like at least int could be checked and other
					if (typeof(bool) == typeof(T))
					{
						return (T)(object)(value == "true");
					}
					else if (typeof(string) == typeof(T))
					{
						return (T)(object)(value);
					}
					else if (typeof(int) == typeof(T))
					{
						return (T)(object)(int.Parse(value));
					}
					else if (typeof(T).IsEnum)
					{
						foreach (T item in Enum.GetValues(typeof(T)))
						{
							if (item.ToString().ToLower().Equals(value.Trim().ToLower()))
								return item;
						}
						_errorHandler.ReportError($"The value '{value}' is invalid for the '{key}' tag");
					}
				}
				else
				{
					return defaultValue; // do not error here!
				}
			}
			catch (Exception ex)
			{
				_errorHandler.ReportError($"Compiler error while inferencing '{key}' tag: {ex}");
			}
			_errorHandler.ReportError($"Compiler error while inferencing '{key}' tag");
			return defaultValue;
		}
	}
}
