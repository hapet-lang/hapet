namespace HapetCompiler.ProjectConf.Data
{
	/// <summary>
	/// This shity class is used to handle shite inside '<ItemGroup>' tags
	/// </summary>
	internal class ProjectData
	{
		public List<string> ProjectReferences { get; private set; } = new List<string>();
	}
}
