using System.Diagnostics;

namespace Frontend
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class SkipInStackFrameAttribute : Attribute
	{ }

	public static class Funcad
	{
		[DebuggerStepThrough]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Exception not required. This is for error reporting only.")]
		public static (string function, string file, int line)? GetCallingFunction()
		{
			try
			{
				var trace = new StackTrace(true);
				var frames = trace.GetFrames();

				foreach (var frame in frames)
				{
					var method = frame.GetMethod();
					var attribute = method.GetCustomAttributesData().FirstOrDefault(d => d.AttributeType == typeof(SkipInStackFrameAttribute));
					if (attribute != null)
						continue;

					return (method.Name, frame.GetFileName(), frame.GetFileLineNumber());
				}
			}
			catch (Exception)
			{ }

			return null;
		}
	}
}
