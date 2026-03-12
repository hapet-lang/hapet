using Microsoft.Language.Xml;

namespace HapetFrontend.Entities
{
    /// <summary>
    /// This shity class is used to handle shite inside '<ItemGroup>' tags
    /// </summary>
    public sealed class ProjectData
    {
        /// <summary>
        /// Path to the project (absolute!) (used usually for calc namespaces)
        /// </summary>
        public string ProjectPath { get; set; }

        /// <summary>
        /// 'true' when another project is build the current one (project reference). 
        /// 'false' if it is the main project that user started to compile
        /// </summary>
        public bool IsReferencedCompilation { get; set; }

        #region PropertyGroup
        /// <summary>
        /// The name of the project that is going to be compiled
        /// </summary>
        public string ProjectName { get; set; }
        /// <summary>
        /// The name of output assembly file. Default is <see cref="ProjectName"/>
        /// </summary>
        public string AssemblyName { get; set; }
        /// <summary>
        /// The version of the project that is going to be compiled
        /// </summary>
        public string ProjectVersion { get; set; }
        /// <summary>
        /// The absolut path to the output directory
        /// </summary>
        public string OutputDirectory { get; set; }
        /// <summary>
        /// If true - pointers usage and other shite are allowed
        /// </summary>
        public bool AllowUnsafeCode { get; set; }
        /// <summary>
        /// The name of the root namespace
        /// </summary>
        public string RootNamespace { get; set; }
        /// <summary>
        /// If true - LLVM IR file would be outputed to out folder
        /// </summary>
        public bool OutputIrFile { get; set; }
        /// <summary>
        /// If true - after lp file would be outputed to out folder
        /// </summary>
        public bool OutputAfterLpFile { get; set; }
        /// <summary>
        /// The optimization level of the project
        /// </summary>
        public int Optimization { get; set; }

        /// <summary>
        /// The format of output - library, console or windowed
        /// </summary>
        public TargetFormat TargetFormat { get; set; }
        #endregion

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
