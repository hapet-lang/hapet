using HapetFrontend.Entities;
using System.Diagnostics;

namespace HapetCompiler.Toolchains
{
    internal sealed class HapetUpdateToolchain
    {
        private readonly Stopwatch _stopwatch;
        public HapetUpdateToolchain(Stopwatch stopwatch)
        {
            _stopwatch = stopwatch;
        }

        public void TryUpdateHapet(IMessageHandler messageHandler)
        {

        }
    }
}
