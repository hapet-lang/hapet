using HapetCommon;
using HapetFrontend;
using HapetFrontend.Entities;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HapetBackend.Llvm.Linkers
{
	public static partial class LlvmLinker
	{
		public static bool Link(Compiler compiler, string targetFile, string objFile, IEnumerable<string> libraryIncludeDirectories, IEnumerable<string> libraries, string subsystem, IErrorHandler errorHandler, bool printLinkerArgs)
		{
			if (compiler is null)
				throw new ArgumentNullException(nameof(compiler));
			if (libraryIncludeDirectories is null)
				throw new ArgumentNullException(nameof(libraryIncludeDirectories));
			if (errorHandler is null)
				throw new ArgumentNullException(nameof(errorHandler));

			string target = null;
			switch (CompilerSettings.TargetPlatformData.TargetPlatform)
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

			filename = Path.Combine(dir, filename);

			var lldArgs = new List<string>();
			lldArgs.Add($"/out:{filename}{CompilerSettings.TargetPlatformData.ExecutableFileExtension}");
			lldArgs.Add("/errorlimit:0");

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

			// other options
			switch (CompilerSettings.TargetPlatformData.TargetPlatform)
			{
				case TargetPlatform.Win86: 
					lldArgs.Add("/entry:mainCRTStartup"); break;
				case TargetPlatform.Win64: 
					lldArgs.Add("/entry:WinMainCRTStartup"); break;
					// TODO: do i need this for linux?
			}
			lldArgs.Add($"/machine:{target}");
			lldArgs.Add($"/subsystem:{subsystem}");

			// link platform specific shite
			if (!LinkPlatformLibraries(lldArgs, errorHandler, target))
				return false;

			foreach (var linc in libraries)
			{
				lldArgs.Add(linc);
			}

			// generated object files
			lldArgs.Add(objFile);

			if (printLinkerArgs)
				Console.WriteLine("[LINKER] " + string.Join(" ", lldArgs.Select(a => $"\"{a}\"")));

			var process = CompilerUtils.StartProcess("lld-link", lldArgs,
							stdout: (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); },
							stderr: (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); });
			process.WaitForExit();
			var result = process.ExitCode == 0;
			if (result)
			{
				Console.WriteLine($"Generated {filename}.exe");
			}
			else
			{
				Console.WriteLine($"Failed to link");
			}

			return result;
		}
	}
}
