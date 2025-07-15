using HapetCompiler.ProjectConf.Data;
using LLVMSharp;
using System.Xml;
using System.Linq;
using HapetFrontend.Errors;

namespace HapetCompiler.ProjectConf
{
    internal sealed partial class ProjectXmlParser
    {
        private void PrepareItemGroups()
        {
            // go all over the item groups
            foreach (var xnode in _itemGroups)
            {
                // TODO: check conditions in project file
                // go all over the project settings
                foreach (XmlNode childnode in xnode.ChildNodes)
                {
                    // skip comments
                    if (childnode is XmlComment)
                        continue;

                    // TODO: check conditions in project file tags
                    switch (childnode.Name)
                    {
                        case "ProjectReference":
                            {
                                // TODO: checks and errors that the attr exists
                                string thePathToProject = childnode.Attributes.GetNamedItem("Include").Value;
                                _projectData.ProjectReferences.Add(thePathToProject);
                                break;
                            }
                        case "Reference":
                            {
                                // TODO: checks and errors that the attr exists
                                string thePathToDll = childnode.Attributes.GetNamedItem("Include").Value;
                                _projectData.References.Add(thePathToDll);
                                break;
                            }
                        case "Define":
                            {
                                string name = childnode.Attributes.GetNamedItem("Name").Value;
                                _projectData.Defines.Add(name, childnode.InnerText);
                                break;
                            }
                        default:
                            {
                                var loc = NodeLocationFinder.GetLocationOfNode(_projectFileText, childnode, _projectPathAbsolute);
                                _messageHandler.ReportMessage(_projectFileText, loc, [childnode.Name], ErrorCode.Get(CTEN.UnexpectedProjectFileTag));
                                break;
                            }
                    }
                }
            }
        }
    }
}
