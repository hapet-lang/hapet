namespace HapetFrontend
{
	public enum TargetPlatform
	{
		Win32,
		Win64,
	}

	public class PlatformData
	{
		public string Name { get; set; }
		public TargetPlatform TargetPlatform { get; set; }
		public int PointerSize { get; set; }
	}

	public static class CompilerSettings
	{
		public static readonly PlatformData[] SupportedPlatforms = 
		{
			new PlatformData() { Name = "win-32", TargetPlatform = TargetPlatform.Win32, PointerSize = 4 },
			new PlatformData() { Name = "win-64", TargetPlatform = TargetPlatform.Win64, PointerSize = 8 },
		};

		public static PlatformData PlatformData { get; set; } 
	}
}
