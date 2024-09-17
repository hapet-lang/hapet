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
		Executable,
		Library
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

		public static PlatformData PlatformData { get; set; } 
		public static TargetFormat TargetFormat { get; set; }
	}
}
