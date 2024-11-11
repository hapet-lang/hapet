using HapetFrontend;
using HapetFrontend.Entities;
using System.Xml;

namespace HapetCompiler.ProjectConf
{
    internal partial class ProjectXmlParser
    {
        private readonly string _projectPath = string.Empty;
        private readonly string _projectPathAbsolute = string.Empty;
        private readonly CompilerSettings _projectSettings = null;
        private readonly IMessageHandler _messageHandler = null;

        /// <summary>
        /// Data of <PropertyGroup> tag
        /// </summary>
        private Dictionary<string, string> _propertyGroupData = new Dictionary<string, string>();
        /// <summary>
        /// All <ItemGroup> tags in .hptproj
        /// </summary>
        private List<XmlElement> _itemGroups = new List<XmlElement>();

        // TODO: this class also should get args from cmd that would have bigger priority over defined in .hptproj file
        public ProjectXmlParser(string projectPath, CompilerSettings projectSettings, IMessageHandler messageHandler)
        {
            _projectPath = projectPath;
            _projectPathAbsolute = Path.GetFullPath(_projectPath);
            _projectSettings = projectSettings;
            _messageHandler = messageHandler;

            XmlDocument projDoc = new XmlDocument();
            projDoc.Load(_projectPath);
            if (projDoc == null)
            {
                _messageHandler.ReportMessage($"Project file {_projectPath} could not be parsed");
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
                        // skip comments
                        if (childnode is XmlComment)
                            continue;
                        _propertyGroupData.Add(childnode.Name, childnode.FirstChild.Value);
                    }
                }
                else if (xnode.Name == "ItemGroup")
                {
                    // just add it and prepare in another file
                    _itemGroups.Add(xnode);
                }
                // TODO: ...
                else
                {
                    _messageHandler.ReportMessage($"Unexpected tag {xnode.Name} in {_projectPath}");
                }
            }

            // setting the project path into the settings
            _projectSettings.ProjectPath = Path.GetFullPath(_projectPath);
        }

        public void PrepareProjectFile()
        {
            UpdateSettings();
		}
    }
}
