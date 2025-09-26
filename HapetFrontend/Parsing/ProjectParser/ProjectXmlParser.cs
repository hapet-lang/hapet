using HapetCompiler.ProjectConf.Data;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using Microsoft.Language.Xml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;

namespace HapetCompiler.ProjectConf
{
    public partial class ProjectXmlParser
    {
        private readonly string _projectPath = string.Empty;
        private readonly string _projectPathAbsolute = string.Empty;
        private readonly string _projectFileText = string.Empty;
        private readonly string[] _projectFileTextSplitted = null;
        private readonly ProgramFile _projectFile;
        private readonly CompilerSettings _projectSettings;
        private readonly ProjectData _projectData;
        private readonly IMessageHandler _messageHandler;

        /// <summary>
        /// All <PropertyGroup> tags in .hptproj
        /// </summary>
        private readonly List<XmlElementSyntax> _propertyGroups = new List<XmlElementSyntax>();
        /// <summary>
        /// All <ItemGroup> tags in .hptproj
        /// </summary>
        private readonly List<XmlElementSyntax> _itemGroups = new List<XmlElementSyntax>();

        // TODO: this class also should get args from cmd that would have bigger priority over defined in .hptproj file
        public ProjectXmlParser(string projectPath, CompilerSettings projectSettings, ProjectData projectData, IMessageHandler messageHandler)
        {
            _projectPath = projectPath;
            _projectPathAbsolute = Path.GetFullPath(_projectPath);
            _projectFileText = File.ReadAllText(_projectPath).Replace("\r\n", "\n");
            _projectFileTextSplitted = _projectFileText.Split('\n');
            _projectFile = new ProgramFile(Path.GetFileName(projectPath), _projectFileText);
            _projectFile.FilePath = new Uri(_projectPathAbsolute);

            _projectSettings = projectSettings;
            _projectData = projectData;
            _messageHandler = messageHandler;

            var projDoc = Parser.ParseText(_projectFileText);
            //#pragma warning disable CA1031 // Do not catch general exception types
            //            try
            //            {
            //                projDoc.Load(_projectPath);
            //            }
            //            catch (Exception e)
            //            {
            //                _messageHandler.ReportMessage([_projectPathAbsolute, e.Message], ErrorCode.Get(CTEN.ProjectFileException));
            //                return;
            //            }
            //#pragma warning restore CA1031 // Do not catch general exception types
            //            if (projDoc.DocumentElement == null)
            //            {
            //                _messageHandler.ReportMessage([_projectPathAbsolute], ErrorCode.Get(CTEN.ProjectFileCouldNotBeParsed));
            //                return;
            //            }

            XmlElementSyntax projRoot = projDoc.Root as XmlElementSyntax;
            foreach (var xnode in projRoot.Content)
            {
                if (xnode is XmlElementSyntax xmlElement)
                {
                    // check that the tag is PropertyGroup
                    if (xmlElement.Name == "PropertyGroup")
                    {
                        // just add it and prepare in another file
                        _propertyGroups.Add(xmlElement);
                        continue;
                    }
                    else if (xmlElement.Name == "ItemGroup")
                    {
                        // just add it and prepare in another file
                        _itemGroups.Add(xmlElement);
                        continue;
                    }
                }

                var loc = NodeLocationFinder.GetLocationOfNode(_projectFileText, xnode, _projectPathAbsolute);
                _messageHandler.ReportMessage(_projectFile, loc, [], ErrorCode.Get(CTEN.UnexpectedProjectFileTag));
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
