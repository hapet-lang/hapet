using HapetFrontend;
using HapetFrontend.Entities;

namespace HapetBackend.Llvm.Linkers
{
    public interface ILinker
    {
        bool Link(Compiler compiler, string targetFile, string objFile,
            IEnumerable<string> libraryIncludeDirectories,
            IEnumerable<string> libraries,
            IMessageHandler messageHandler,
            out string outFilePath);
    }
}
