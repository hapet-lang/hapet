namespace HapetFrontend
{
	public enum TargetPlatform
	{
		Win32,
		Win64,
	}

	public static class CompilerSettings
	{
		public static TargetPlatform TargetPlatform { get; set; } 
	}
}
