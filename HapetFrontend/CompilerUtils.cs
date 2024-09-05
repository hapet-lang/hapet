using HapetFrontend.Ast;
using HapetFrontend.Entities;

namespace HapetFrontend
{
	public static class CompilerUtils
	{
		#region Extensions
		public static string PathNormalize(this string path)
		{
			return Path.GetFullPath(new Uri(path).LocalPath)
					   .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
