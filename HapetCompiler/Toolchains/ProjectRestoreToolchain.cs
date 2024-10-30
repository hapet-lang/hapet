using HapetFrontend.Entities;
namespace HapetCompiler.Toolchains
{
	internal class ProjectRestoreToolchain
	{
		private string[] _cmdArgs; // TODO: use them for ProjectXmlParser
		public ProjectRestoreToolchain(string[] args)
		{
			_cmdArgs = args;
		}

		public int Restore(string projectPath, IMessageHandler messageHandler)
		{
			return 0;
		}
	}
}
