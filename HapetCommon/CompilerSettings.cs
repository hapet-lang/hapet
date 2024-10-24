using System.Runtime.InteropServices;

namespace HapetCommon
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

	public static class CompilerSettings
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
		/// The platform on which compiled binaries are going to be running
		/// </summary>
		public static PlatformData TargetPlatformData { get; set; } 
		/// <summary>
		/// The platform on which compiler is running
		/// </summary>
		public static PlatformData CurrentPlatformData { get; set; }
		/// <summary>
		/// The format of output - library, console or windowed
		/// </summary>
		public static TargetFormat TargetFormat { get; set; }


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
