using HapetCompiler.ProjectConf.Data;
using LLVMSharp;
using System.Xml;
using System.Linq;

namespace HapetCompiler.ProjectConf
{
	internal partial class ProjectXmlParser
	{
		private void PrepareItemGroups()
		{
			// go all over the item groups
			foreach (var xnode in _itemGroups)
			{
				// TODO: check conditions
				// go all over the project settings
				foreach (XmlNode childnode in xnode.ChildNodes)
				{
					// skip comments
					if (childnode is XmlComment)
						continue;
					
					// TODO: check conditions
					switch (childnode.Name)
					{
						case "ProjectReference":
							{
								// TODO: checks and errors that the attr exists
								string thePathToProject = childnode.Attributes.GetNamedItem("Include").Value;
								_projectData.ProjectReferences.Add(thePathToProject);
								break;
							}
						default:
							{
                                var loc = NodeLocationFinder.GetLocationOfNode(_projectFileText, childnode, _projectPathAbsolute);
                                _messageHandler.ReportMessage(_projectFileText, loc, $"Unexpected tag {childnode.Name}");
								break;
							}
					}
				}
			}
		}
	}
}
