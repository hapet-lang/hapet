using HapetFrontend;
using System.Xml.Linq;
using System.Xml;
using HapetCompiler.ProjectConf.Data;
using HapetFrontend.Errors;

namespace HapetCompiler.ProjectConf
{
    internal partial class ProjectXmlParser
    {
        /// <summary>
        /// Data of <PropertyGroup> tag
        /// </summary>
        private Dictionary<string, (string, XmlNode)> _propertyGroupData = new Dictionary<string, (string, XmlNode)>();

        private void PreparePropertyGroups()
        {
            // go all over the prop groups
            foreach (var xnode in _propertyGroups)
            {
                // TODO: check conditions
                // go all over the project settings
                foreach (XmlNode childnode in xnode.ChildNodes)
                {
                    // skip comments
                    if (childnode is XmlComment)
                        continue;
                    // TODO: check conditions
                    _propertyGroupData.Add(childnode.Name, (childnode.FirstChild.Value, childnode));
                }
            }
            UpdateSettings();
        }

        private void UpdateSettings()
        {
            // setting project name
            string projectFileName = Path.GetFileNameWithoutExtension(_projectPath);
            _projectSettings.ProjectName = GetValueOrDefault("ProjectName", projectFileName);
            // setting project version
            _projectSettings.ProjectVersion = GetValueOrDefault("ProjectVersion", "1.0.0");
            // setting project configuration
            _projectSettings.ProjectConfiguration = GetValueOrDefault("ProjectConfiguration", "Debug");
            // setting project out folder
            var outDirRelative = GetValueOrDefault("OutputDirectory", $"./bin/{_projectSettings.ProjectConfiguration}");
            _projectSettings.OutputDirectory = $"{Path.GetDirectoryName(_projectPathAbsolute).Replace("\\", "/").TrimEnd('/')}/{outDirRelative.Replace("\\", "/").TrimEnd('/')}";
            // WARN: creating the dir here!!!
            if (!Directory.Exists(_projectSettings.OutputDirectory)) Directory.CreateDirectory(_projectSettings.OutputDirectory);
            // setting the root namespace
            _projectSettings.RootNamespace = GetValueOrDefault("RootNamespace", _projectSettings.ProjectName);

            // setting unsafe code allowence
            _projectSettings.AllowUnsafeCode = GetValueOrDefault("AllowUnsafeCode", false);
            // setting llvm ir code outputance
            _projectSettings.OutputIrFile = GetValueOrDefault("OutputIrFile", false);
            // setting verbose enablence :)
            _projectSettings.Verbose = GetValueOrDefault("Verbose", false);
            // setting the optimization level
            _projectSettings.Optimization = GetValueOrDefault("Optimization", 3);

            // setting target format
            _projectSettings.TargetFormat = GetValueOrDefault("TargetFormat", TargetFormat.Console);
            // setting platform data
            string targetPlatform = GetValueOrDefault("TargetPlatform", "");
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
                if (_propertyGroupData.TryGetValue(key, out var tuple))
                {
                    string value = tuple.Item1;
                    // TODO: better casts. like at least int could be checked and other
                    if (typeof(bool) == typeof(T))
                    {
                        return (T)(object)(value == "true");
                    }
                    else if (typeof(string) == typeof(T))
                    {
                        return (T)(object)value;
                    }
                    else if (typeof(int) == typeof(T))
                    {
                        bool parsed = int.TryParse(value, out int outV);
                        if (!parsed)
                        {
                            var loc = NodeLocationFinder.GetLocationOfNode(_projectFileText, tuple.Item2, _projectPathAbsolute);
                            _messageHandler.ReportMessage(_projectFileText, loc, [value], ErrorCode.Get(CTEN.TagNotParsedToInt));
                            return (T)(object)0;
                        }
                        return (T)(object)outV;
                    }
                    else if (typeof(T).IsEnum)
                    {
                        foreach (T item in Enum.GetValues(typeof(T)))
                        {
                            if (item.ToString().ToLower().Equals(value.Trim().ToLower()))
                                return item;
                        }
                        var loc = NodeLocationFinder.GetLocationOfNode(_projectFileText, tuple.Item2, _projectPathAbsolute);
                        _messageHandler.ReportMessage(_projectFileText, loc, [value, key], ErrorCode.Get(CTEN.ValueInvalidForTag));
                    }
                }
                else
                {
                    return defaultValue; // do not error here!
                }
            }
            catch (Exception ex)
            {
                _messageHandler.ReportMessage([key, $": {ex}"], ErrorCode.Get(CTEN.ErrorInferencingProjTag));
            }
            _messageHandler.ReportMessage([key, ""], ErrorCode.Get(CTEN.ErrorInferencingProjTag));
            return defaultValue;
        }
    }
}
