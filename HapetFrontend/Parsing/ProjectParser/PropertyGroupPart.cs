using HapetFrontend;
using System.Xml.Linq;
using System.Xml;
using HapetFrontend.Errors;
using System.Globalization;
using Microsoft.Language.Xml;

namespace HapetFrontend.ProjectParser
{
    public partial class ProjectXmlParser
    {
        /// <summary>
        /// Data of <PropertyGroup> tag
        /// </summary>
        private readonly Dictionary<string, (string, IXmlElement)> _propertyGroupData = new Dictionary<string, (string, IXmlElement)>();

        private void PreparePropertyGroups()
        {
            _propertyGroupData.Clear();

            // go all over the prop groups
            foreach (var xnode in _propertyGroups)
            {
                // TODO: check conditions in proj file tag
                // go all over the project settings
                foreach (var childnode in xnode.Content)
                {
                    // skip comments
                    if (childnode is XmlCommentSyntax)
                        continue;
                    // TODO: check conditions in proj file tag
                    if (childnode is IXmlElementSyntax xmlElement)
                    {
                        _propertyGroupData.Add(xmlElement.Name, ((xmlElement.Content.First() as XmlTextSyntax).Value, xmlElement.AsElement));
                    }
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
            _projectSettings.OutputDirectory = $"{Path.GetDirectoryName(_projectPathAbsolute).Replace("\\", "/", StringComparison.InvariantCulture).TrimEnd('/')}/{outDirRelative.Replace("\\", "/", StringComparison.InvariantCulture).TrimEnd('/')}";
            // WARN: creating the dir here!!!
            if (!Directory.Exists(_projectSettings.OutputDirectory)) Directory.CreateDirectory(_projectSettings.OutputDirectory);
            // setting the root namespace
            _projectSettings.RootNamespace = GetValueOrDefault("RootNamespace", _projectSettings.ProjectName);
            // setting assembly name
            _projectSettings.AssemblyName = GetValueOrDefault("AssemblyName", _projectSettings.ProjectName);

            // setting unsafe code allowence
            _projectSettings.AllowUnsafeCode = GetValueOrDefault("AllowUnsafeCode", false);
            // setting llvm ir code outputance
            _projectSettings.OutputIrFile = GetValueOrDefault("OutputIrFile", false);
            // setting after lp code outputance
            _projectSettings.OutputAfterLpFile = GetValueOrDefault("OutputAfterLpFile", false);
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

            // ...
        }

        // TODO: add allowed items parameter
        // for example when parsing ProjectConfiguration should be checked
        private T GetValueOrDefault<T>(string key, T defaultValue)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                if (_propertyGroupData.TryGetValue(key, out var tuple))
                {
                    string value = tuple.Item1;
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
                            var loc = _projectFile.GetLocationFromSpan(tuple.Item2.Start, tuple.Item2.Start + tuple.Item2.FullWidth);
                            _messageHandler.ReportMessage(_projectFile, loc, [value], ErrorCode.Get(CTEN.TagNotParsedToInt));
                            return (T)(object)0;
                        }
                        return (T)(object)outV;
                    }
                    else if (typeof(T).IsEnum)
                    {
                        foreach (T item in Enum.GetValues(typeof(T)))
                        {
                            if (item.ToString().ToUpperInvariant().Equals(value.Trim().ToUpperInvariant(), StringComparison.Ordinal))
                                return item;
                        }
                        var loc = _projectFile.GetLocationFromSpan(tuple.Item2.Start, tuple.Item2.Start + tuple.Item2.FullWidth);
                        _messageHandler.ReportMessage(_projectFile, loc, [value, key], ErrorCode.Get(CTEN.ValueInvalidForTag));
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
#pragma warning restore CA1031 // Do not catch general exception types
            _messageHandler.ReportMessage([key, ""], ErrorCode.Get(CTEN.ErrorInferencingProjTag));
            return defaultValue;
        }
    }
}
