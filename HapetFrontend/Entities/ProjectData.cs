using Microsoft.Language.Xml;

namespace HapetFrontend.Entities
{
    /// <summary>
    /// This shity class is used to handle shite inside '<ItemGroup>' tags
    /// </summary>
    public sealed class ProjectData
    {
        public List<Reference> References { get; private set; } = new List<Reference>();
        public List<Reference> ProjectReferences { get; private set; } = new List<Reference>();

        /// <summary>
        /// Handles all the define's that could be accessed accross the whole project
        /// </summary>
        public Dictionary<string, string> Defines { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Contains all the project names that are referenced
        /// </summary>
        public List<string> AllReferencedProjectNames { get; private set; } = new List<string>();
    }

    public sealed class Reference
    { 
        public string ReferenceName { get; set; }
        public IXmlElementSyntax Node { get; set; }

        public Reference(string reference, IXmlElementSyntax node) 
        {
            ReferenceName = reference;
            Node = node;
        }
    }
}
