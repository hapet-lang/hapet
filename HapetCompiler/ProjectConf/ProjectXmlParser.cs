using HapetCompiler.ProjectConf.Data;
using HapetFrontend;
using HapetFrontend.Entities;
using System;
using System.Xml;

namespace HapetCompiler.ProjectConf
{
    internal partial class ProjectXmlParser
    {
        private readonly string _projectPath = string.Empty;
        private readonly string _projectPathAbsolute = string.Empty;
        private readonly string _projectFileText = string.Empty;
        private readonly CompilerSettings _projectSettings = null;
        private readonly ProjectData _projectData = null;
        private readonly IMessageHandler _messageHandler = null;

		/// <summary>
		/// All <PropertyGroup> tags in .hptproj
		/// </summary>
		private List<XmlElement> _propertyGroups = new List<XmlElement>();
		/// <summary>
		/// All <ItemGroup> tags in .hptproj
		/// </summary>
		private List<XmlElement> _itemGroups = new List<XmlElement>();

        // TODO: this class also should get args from cmd that would have bigger priority over defined in .hptproj file
        public ProjectXmlParser(string projectPath, CompilerSettings projectSettings, ProjectData projectData, IMessageHandler messageHandler)
        {
            _projectPath = projectPath;
            _projectPathAbsolute = Path.GetFullPath(_projectPath);
            _projectFileText = File.ReadAllText(_projectPath).Replace("\t", "    ");
			_projectSettings = projectSettings;
            _projectData = projectData;
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
					// just add it and prepare in another file
					_propertyGroups.Add(xnode);
				}
                else if (xnode.Name == "ItemGroup")
                {
                    // just add it and prepare in another file
                    _itemGroups.Add(xnode);
                }
                // TODO: ...
                else
                {
                    var loc = NodeLocationFinder.GetLocationOfNode(_projectFileText, xnode, _projectPathAbsolute);
                    _messageHandler.ReportMessage(_projectFileText, loc, $"Unexpected tag {xnode.Name}");
                }
            }

            // setting the project path into the settings
            _projectSettings.ProjectPath = Path.GetFullPath(_projectPath);
        }

        public void PrepareProjectFile()
        {
            PreparePropertyGroups();
            PrepareItemGroups();
		}
    }
}
