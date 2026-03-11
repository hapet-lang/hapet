using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using System.Reflection;

namespace HapetBackend.Llvm.Linkers.Windows
{
    public partial class WinLinker : ILinker
    {
        private Compiler _compiler;

        public bool Link(Compiler compiler, string targetFile, string objFile, 
            IEnumerable<string> libraryIncludeDirectories, 
            IEnumerable<string> libraries, 
            IMessageHandler messageHandler,
            out string outFilePath)
        {
            ArgumentNullException.ThrowIfNull(compiler);
            ArgumentNullException.ThrowIfNull(libraryIncludeDirectories);
            ArgumentNullException.ThrowIfNull(messageHandler);

            _compiler = compiler;
            bool verbose = _compiler.CurrentProjectSettings.Verbose;

            string target = null;
            switch (compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform)
            {
                case TargetPlatform.Win86:
                case TargetPlatform.Linux86:
                    target = "x86"; break;
                case TargetPlatform.Win64:
                case TargetPlatform.Linux64:
                    target = "x64"; break;
            }

            // creating executable name
            var filename = Path.GetFileNameWithoutExtension(targetFile + ".x");
            var dir = Path.GetDirectoryName(Path.GetFullPath(targetFile));
            var outFileExtension = compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Library ?
                compiler.CurrentProjectSettings.TargetPlatformData.LibraryFileExtension :
                compiler.CurrentProjectSettings.TargetPlatformData.ExecutableFileExtension;

            filename = Path.Combine(dir, filename);
            var lldArgs = new List<string>();
            lldArgs.Add($"/out:{filename}{outFileExtension}");
            if (compiler.CurrentProjectSettings.TargetFormat != TargetFormat.Console && compiler.CurrentProjectSettings.TargetFormat != TargetFormat.Windowed)
                lldArgs.Add($"/IMPLIB:{filename}.lib");

            // current compiler directory
            var exePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            lldArgs.Add($"-libpath:{exePath}");

            foreach (var linc in libraryIncludeDirectories)
            {
                lldArgs.Add($@"-libpath:{linc}");
            }

            // we need to set entry only for console and windowed types
            if (compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Console || compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Windowed)
            {
                // other options
                switch (compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform)
                {
                    case TargetPlatform.Win86:
                    case TargetPlatform.Win64:
                        lldArgs.Add("/entry:mainCRTStartup");
                        break;
                        // TODO: do i need this for linux?
                }
            }

            lldArgs.Add($"/machine:{target}");
            if (compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Console || compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Windowed)
                lldArgs.Add($"/subsystem:console"); // WARN: always console because the want 'int main(int argc, char*[] argv)'
            else
                lldArgs.Add($"/DLL"); // TODO: is it ok for linux and other?

            // generated object files
            lldArgs.Add(objFile);

            // link platform specific shite
            if (!LinkPlatformLibraries(compiler, lldArgs, messageHandler, target))
            {
                outFilePath = "";
                return false;
            }

            foreach (var linc in libraries)
            {
                lldArgs.Add(linc);
            }

            // searching for linker
            string vsLinkerFile = Path.Combine(CompilerUtils.CurrentHapetDirectory, "lld-link.exe");
            if (!File.Exists(vsLinkerFile))
            {
                messageHandler.ReportMessage([], ErrorCode.Get(CTEN.NoLldLinker));
                outFilePath = "";
                return false;
            }

            if (verbose)
                Console.WriteLine("[LINKER] " + vsLinkerFile + " " + string.Join(" ", lldArgs.Select(a => $"\"{a}\"")));

            var process = Funcad.StartProcess(vsLinkerFile, lldArgs,
                            stdout: (s, e) =>
                            {
                                if (e.Data != null && verbose)
                                    Console.WriteLine(e.Data);
                            },
                            stderr: (s, e) =>
                            {
                                if (e.Data != null)
                                    messageHandler.ReportMessage([e.Data], ErrorCode.Get(CTEN.LinkerItselfError), ReportType.Error);
                            });
            process.WaitForExit();
            var result = process.ExitCode == 0;
            if (result)
            {
                // print if it is not a referenced compilation
                if (!_compiler.CurrentProjectSettings.IsReferencedCompilation && !CompilerSettings.IsInRunContext)
                    messageHandler.ReportMessage([$"\t  Generated {filename}{outFileExtension}"], null, ReportType.Info);
            }
            else
            {
                messageHandler.ReportMessage([], ErrorCode.Get(CTEN.FailedToLink), ReportType.Error);
            }

            outFilePath = $"{filename}{outFileExtension}";
            return result;
        }
    }
}
