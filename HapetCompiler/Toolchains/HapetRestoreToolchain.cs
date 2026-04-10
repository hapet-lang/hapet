using HapetFrontend.Entities;
namespace HapetCompiler.Toolchains
{
    internal sealed class HapetRestoreToolchain
    {
        private string[] _cmdArgs; // TODO: use them for ProjectXmlParser
        public HapetRestoreToolchain(string[] args)
        {
            _cmdArgs = args;
        }

        public int Restore(string projectPath, IMessageHandler messageHandler)
        {
            return 0;
        }
    }
}
