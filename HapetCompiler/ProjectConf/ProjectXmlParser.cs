using HapetCompiler.ProjectConf.Data;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using System;
using System.Collections.Generic;
using System.Xml;

namespace HapetCompiler.ProjectConf
{
    internal partial class ProjectXmlParser
    {
        private readonly string _projectPath = string.Empty;
        private readonly string _projectPathAbsolute = string.Empty;
        private readonly string _projectFileText = string.Empty;
        private readonly ProgramFile _projectFile;
        private readonly CompilerSettings _projectSettings;
        private readonly ProjectData _projectData;
        private readonly IMessageHandler _messageHandler;

        /// <summary>
        /// All <PropertyGroup> tags in .hptproj
        /// </summary>
        private readonly List<XmlElement> _propertyGroups = new List<XmlElement>();
        /// <summary>
        /// All <ItemGroup> tags in .hptproj
        /// </summary>
        private readonly List<XmlElement> _itemGroups = new List<XmlElement>();

        // TODO: this class also should get args from cmd that would have bigger priority over defined in .hptproj file
        public ProjectXmlParser(string projectPath, CompilerSettings projectSettings, ProjectData projectData, IMessageHandler messageHandler)
        {
            _projectPath = projectPath;
            _projectPathAbsolute = Path.GetFullPath(_projectPath);
            _projectFileText = File.ReadAllText(_projectPath).Replace("\t", "    ", StringComparison.InvariantCulture);
            _projectFile = new ProgramFile(Path.GetFileName(projectPath), _projectFileText);
            _projectFile.FilePath = _projectPathAbsolute;

            _projectSettings = projectSettings;
            _projectData = projectData;
            _messageHandler = messageHandler;

            XmlDocument projDoc = new XmlDocument();
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                projDoc.Load(_projectPath);
            }
            catch (Exception e)
            {
                _messageHandler.ReportMessage([_projectPathAbsolute, e.Message], ErrorCode.Get(CTEN.ProjectFileException));
                return;
            }
#pragma warning restore CA1031 // Do not catch general exception types
            if (projDoc.DocumentElement == null)
            {
                _messageHandler.ReportMessage([_projectPathAbsolute], ErrorCode.Get(CTEN.ProjectFileCouldNotBeParsed));
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
                // ...
                else
                {
                    var loc = NodeLocationFinder.GetLocationOfNode(_projectFileText, xnode, _projectPathAbsolute);
                    _messageHandler.ReportMessage(_projectFile, loc, [xnode.Name], ErrorCode.Get(CTEN.UnexpectedProjectFileTag));
                }
            }

            // setting the project path into the settings
            _projectSettings.ProjectPath = Path.GetFullPath(_projectPath);
        }

        public void PrepareProjectFile()
        {
            PreparePropertyGroups();
            PrepareItemGroups();
            SetDefaultDefines();
        }
    }
}
