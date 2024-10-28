using System.Runtime.InteropServices;

namespace HapetFrontend
{
	public enum TargetPlatform
	{
		Win86,
		Win64,
		Linux86,
		Linux64,
	}

	public enum TargetFormat
	{
		Library,
		Console,
		Windowed
	}

	public class PlatformData
	{
		public string Name { get; set; }
		public TargetPlatform TargetPlatform { get; set; }
		public int PointerSize { get; set; }
		public string ObjectFileExtension { get; set; }
		public string ExecutableFileExtension { get; set; }
	}

	public class CompilerSettings
	{
		public static readonly PlatformData[] SupportedPlatforms =
		{
			new PlatformData()
			{
				Name = "win-32", TargetPlatform = TargetPlatform.Win86,
				PointerSize = 4,
				ObjectFileExtension = ".obj",
				ExecutableFileExtension = ".exe",
			},
			new PlatformData() 
			{ 
				Name = "win-64", TargetPlatform = TargetPlatform.Win64, 
				PointerSize = 8,
				ObjectFileExtension = ".obj",
				ExecutableFileExtension = ".exe",
			},
			new PlatformData() 
			{ 
				Name = "linux-32", TargetPlatform = TargetPlatform.Linux86, 
				PointerSize = 4,
				ObjectFileExtension = ".o",
				ExecutableFileExtension = "",
			},
			new PlatformData() 
			{ 
				Name = "linux-64", TargetPlatform = TargetPlatform.Linux64, 
				PointerSize = 8,
				ObjectFileExtension = ".o",
				ExecutableFileExtension = "",
			},
		};
        /// <summary>
        /// Path to the project (absolute!) (used usually for calc namespaces)
        /// </summary>
        public string ProjectPath { get; set; } 

        /// <summary>
        /// The name of the project that is going to be compiled
        /// </summary>
        public string ProjectName { get; set; }
		/// <summary>
		/// The version of the project that is going to be compiled
		/// </summary>
		public string ProjectVersion { get; set; }
		/// <summary>
		/// The configuration of project. Debug or Release
		/// </summary>
		public string ProjectConfiguration { get; set; }
		/// <summary>
		/// The absolut path to the output directory
		/// </summary>
		public string OutputDirectory { get; set; }
		/// <summary>
		/// If true - pointers usage and other shite are allowed
		/// </summary>
		public bool AllowUnsafeCode { get; set; }

		/// <summary>
		/// 'true' if Debug conf. 'false' if Release
		/// </summary>
		public bool IsDebug => ProjectConfiguration == "Debug";
		/// <summary>
		/// The platform on which compiled binaries are going to be running
		/// </summary>
		public PlatformData TargetPlatformData { get; set; } 
		/// <summary>
		/// The format of output - library, console or windowed
		/// </summary>
		public TargetFormat TargetFormat { get; set; }


		/// <summary>
		/// The platform on which compiler is running
		/// </summary>
		public static PlatformData CurrentPlatformData { get; set; }
		public static void InitCurrentPlatformData()
		{
			switch (RuntimeInformation.OSArchitecture)
			{
				case Architecture.X86:
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
						CurrentPlatformData = SupportedPlatforms.FirstOrDefault(x => x.TargetPlatform == TargetPlatform.Win86);
					else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
						CurrentPlatformData = SupportedPlatforms.FirstOrDefault(x => x.TargetPlatform == TargetPlatform.Linux86);
					break;
				case Architecture.X64:
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
						CurrentPlatformData = SupportedPlatforms.FirstOrDefault(x => x.TargetPlatform == TargetPlatform.Win64);
					else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
						CurrentPlatformData = SupportedPlatforms.FirstOrDefault(x => x.TargetPlatform == TargetPlatform.Linux64);
					break;
				default:
					// not supported
					break;
			}
		}
	}
}
