namespace HapetFrontend.Entities
{
    /// <summary>
    /// This shity class is used to handle shite inside '<ItemGroup>' tags
    /// </summary>
    public sealed class ProjectData
    {
        public List<string> References { get; private set; } = new List<string>();
        public List<string> ProjectReferences { get; private set; } = new List<string>();

        /// <summary>
        /// Handles all the define's that could be accessed accross the whole project
        /// </summary>
        public Dictionary<string, string> Defines { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Contains all the project names that are referenced
        /// </summary>
        public List<string> AllReferencedProjectNames { get; private set; } = new List<string>();
    }
}
