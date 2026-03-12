using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using Microsoft.Language.Xml;

namespace HapetFrontend.ProjectParser
{
    public partial class ProjectXmlParser
    {
        private readonly string _projectPath = string.Empty;
        private readonly string _projectPathAbsolute = string.Empty;
        private string _projectFileText = string.Empty;
        
        private ProgramFile _projectFile;
        private readonly ProjectData _projectData;
        private readonly IMessageHandler _messageHandler;
        private XmlDocumentSyntax _parsedProjectFile;

        public ProgramFile XmlProgramFile => _projectFile;
        public XmlDocumentSyntax XmlParsed => _parsedProjectFile;
        public string ProjectFileText => _projectFileText;

        /// <summary>
        /// All <PropertyGroup> tags in .hptproj
        /// </summary>
        private readonly List<XmlElementSyntax> _propertyGroups = new List<XmlElementSyntax>();
        /// <summary>
        /// All <ItemGroup> tags in .hptproj
        /// </summary>
        private readonly List<XmlElementSyntax> _itemGroups = new List<XmlElementSyntax>();

        // TODO: this class also should get args from cmd that would have bigger priority over defined in .hptproj file
        public ProjectXmlParser(string projectPath, ProjectData projectData, IMessageHandler messageHandler)
        {
            _projectPath = projectPath;
            if (!File.Exists(_projectPath))
            {
                messageHandler.ReportMessage([_projectPath], ErrorCode.Get(CTEN.FullPathToHapetFileNotFound));
                return;
            }

            _projectPathAbsolute = Path.GetFullPath(_projectPath);
            ParseFile();

            _projectData = projectData;
            _messageHandler = messageHandler;

            PrepareFile();

            // setting the project path into the settings
            _projectData.ProjectPath = Path.GetFullPath(_projectPath);
        }

        /// <summary>
        /// Used only in LSP!!!
        /// </summary>
        /// <param name="text"></param>
        public void SetProjectFileText(string text)
        {
            _projectFileText = text.Replace("\r\n", "\n");
        }

        public void ParseFile(string text = "")
        {
            // reading from file if no text presented
            if (string.IsNullOrWhiteSpace(text))
                _projectFileText = File.ReadAllText(_projectPath);
            else
                _projectFileText = text;
            _projectFileText = _projectFileText.Replace("\r\n", "\n");

            // creating new program file if there was no
            if (_projectFile == null)
                _projectFile = new ProgramFile(Path.GetFileName(_projectPath), new System.Text.StringBuilder(_projectFileText));
            else
                _projectFile.Text = new System.Text.StringBuilder(_projectFileText);

            _projectFile.TextSplitted = _projectFileText.Split('\n');
            _projectFile.FilePath = new Uri(_projectPathAbsolute);

            _parsedProjectFile = Parser.ParseText(_projectFileText);
        }

        public void PrepareFile()
        {
            _propertyGroups.Clear();
            _itemGroups.Clear();

            XmlElementSyntax projRoot = _parsedProjectFile.Root as XmlElementSyntax;
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

                var loc = _projectFile.GetLocationFromSpan(xnode.Span.Start, xnode.Span.End);
                _messageHandler.ReportMessage(_projectFile, loc, [], ErrorCode.Get(CTEN.UnexpectedProjectFileTag));
            }
        }

        public void PrepareProjectFile()
        {
            // just return - it errored somewhere before
            if (!File.Exists(_projectPath))
                return;

            PreparePropertyGroups();
            PrepareItemGroups();
            SetDefaultDefines();
        }
    }
}
