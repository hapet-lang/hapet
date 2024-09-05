using HapetFrontend.Ast;
using HapetFrontend.Entities;
using System.Diagnostics;

namespace HapetFrontend
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class SkipInStackFrameAttribute : Attribute
	{ }

	public static class CompilerUtils
	{
		#region Extensions
		public static string PathNormalize(this string path)
		{
			return Path.GetFullPath(new Uri(path).LocalPath)
					   .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}
		#endregion

		#region Parsing shite helpers
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
		#endregion

		public static bool ValidateFilePath(string dir, string filePath, bool isRel, IErrorHandler eh, (string file, ILocation loc)? from, out string path)
		{
			path = filePath;

			var extension = Path.GetExtension(path);
			if (string.IsNullOrEmpty(extension))
			{
				path += ".hpt";
			}
			else if (extension != ".hpt")
			{
				eh.ReportError($"Invalid extension '{extension}'. Hapet source files must have the extension .hpt");
				return false;
			}

			if (isRel)
			{
				path = Path.Combine(dir, path);
			}

			path = Path.GetFullPath(path);
			path = path.PathNormalize();

			if (!File.Exists(path))
			{
				if (from != null)
				{
					eh.ReportError(from.Value.file, from.Value.loc, $"File '{path}' does not exist");
				}
				else
				{
					eh.ReportError($"File '{path}' does not exist");
				}

				return false;
			}

			return true;
		}
	}
}
