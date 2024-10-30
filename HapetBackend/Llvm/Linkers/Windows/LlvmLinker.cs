using HapetFrontend;
using HapetFrontend.Entities;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HapetBackend.Llvm.Linkers.Windows
{
	public static partial class WinLinker
	{
		public static bool Link(Compiler compiler, string targetFile, string objFile, IEnumerable<string> libraryIncludeDirectories, IEnumerable<string> libraries, IMessageHandler messageHandler, bool printLinkerArgs)
		{
			if (compiler is null)
				throw new ArgumentNullException(nameof(compiler));
			if (libraryIncludeDirectories is null)
				throw new ArgumentNullException(nameof(libraryIncludeDirectories));
			if (messageHandler is null)
				throw new ArgumentNullException(nameof(messageHandler));

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

			// TODO: get libraries of project
			libraries = libraries.Distinct();

			// creating executable name
			var filename = Path.GetFileNameWithoutExtension(targetFile + ".x");
			var dir = Path.GetDirectoryName(Path.GetFullPath(targetFile));
			var outFileExtension = compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Library ? 
				compiler.CurrentProjectSettings.TargetPlatformData.LibraryFileExtension : 
				compiler.CurrentProjectSettings.TargetPlatformData.ExecutableFileExtension;

			filename = Path.Combine(dir, filename);
			var lldArgs = new List<string>();
			lldArgs.Add($"/out:{filename}{outFileExtension}");
			// lldArgs.Add("/errorlimit:0"); // gives me a warning

			// current compiler directory
			var exePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
			lldArgs.Add($"-libpath:{exePath}");

			foreach (var linc in libraryIncludeDirectories)
			{
				lldArgs.Add($@"-libpath:{linc}");
			}

			// TODO: uncomment for std lib
			//lldArgs.Add($@"-libpath:{Environment.CurrentDirectory}\lib"); // hack so it can be used from prj/sln dir
			//lldArgs.Add($@"-libpath:{exePath}\lib");

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
				lldArgs.Add($"/DLL"); // TODO: is it ok?

			// link platform specific shite
			if (!LinkPlatformLibraries(compiler, lldArgs, messageHandler, target))
				return false;

			foreach (var linc in libraries)
			{
				lldArgs.Add(linc);
			}

			// generated object files
			lldArgs.Add(objFile);

			// searching for win linker
			string vsBinFolder = FindVisualStudioBinaryDirectory();
			if (vsBinFolder == null || !Directory.Exists(vsBinFolder))
			{
				messageHandler.ReportMessage("Couldn't find Visual Studio binary directory");
				return false;
			}
			string vsLinkerFolder = $"{vsBinFolder}\\Host{target}\\{target}";
			if (!Directory.Exists(vsLinkerFolder))
			{
				messageHandler.ReportMessage("Couldn't find Visual Studio host bin directory");
				return false;
			}
			string vsLinkerFile = $"{vsLinkerFolder}\\link.exe";
			if (!File.Exists(vsLinkerFile))
			{
				messageHandler.ReportMessage("Couldn't find Visual Studio linker file");
				return false;
			}

			if (printLinkerArgs)
				Console.WriteLine("[LINKER] " + vsLinkerFile + string.Join(" ", lldArgs.Select(a => $"\"{a}\"")));

			var process = CompilerUtils.StartProcess(vsLinkerFile, lldArgs,
							stdout: (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); },
							stderr: (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); });
			process.WaitForExit();
			var result = process.ExitCode == 0;
			if (result)
			{
				Console.WriteLine($"Generated {filename}{outFileExtension}");
			}
			else
			{
				Console.WriteLine($"Failed to link");
			}

			return result;
		}
	}
}
