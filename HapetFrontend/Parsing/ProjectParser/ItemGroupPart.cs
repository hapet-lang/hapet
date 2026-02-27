using System.Xml;
using System.Linq;
using HapetFrontend.Errors;
using Microsoft.Language.Xml;

namespace HapetFrontend.ProjectParser
{
    public partial class ProjectXmlParser
    {
        private void PrepareItemGroups()
        {
            _projectData.ProjectReferences.Clear();
            _projectData.References.Clear();
            _projectData.Defines.Clear();

            // go all over the item groups
            foreach (var xnode in _itemGroups)
            {
                // TODO: check conditions in project file
                // go all over the project settings
                foreach (var childnode in xnode.Content)
                {
                    // skip comments
                    if (childnode is XmlCommentSyntax)
                        continue;

                    if (childnode is not IXmlElementSyntax xmlElement)
                        continue;

                    // TODO: check conditions in project file tags
                    switch (xmlElement.Name)
                    {
                        case "ProjectReference":
                            {
                                // TODO: checks and errors that the attr exists
                                string thePathToProject = xmlElement.GetAttribute("Include").Value;
                                _projectData.ProjectReferences.Add(new Entities.Reference(thePathToProject, xmlElement));
                                break;
                            }
                        case "Reference":
                            {
                                // TODO: checks and errors that the attr exists
                                string thePathToDll = xmlElement.GetAttribute("Include").Value;
                                _projectData.References.Add(new Entities.Reference(thePathToDll, xmlElement));
                                break;
                            }
                        case "Define":
                            {
                                string name = xmlElement.GetAttribute("Name").Value;
                                var content = (xmlElement.Content.First() as XmlTextSyntax).Value;
                                _projectData.Defines.Add(name, content);
                                break;
                            }
                        default:
                            {
                                var loc = _projectFile.GetLocationFromSpan(xmlElement.AsElement.Start, xmlElement.AsElement.Start + xmlElement.AsElement.FullWidth);
                                _messageHandler.ReportMessage(_projectFile, loc, [], ErrorCode.Get(CTEN.UnexpectedProjectFileTag));
                                break;
                            }
                    }
                }
            }
        }
    }
}
