using Frontend.Errors;
using Frontend.Parsing.Entities;

namespace Frontend
{
	public partial class Compiler
	{
		private Dictionary<string, PtFile> _files = new Dictionary<string, PtFile>();
		private Dictionary<string, string> _strings = new Dictionary<string, string>();

		public PtFile AddFile(string fileNameT, string body = null, bool globalScope = false)
		{
			if (!ValidateFilePath("", fileNameT, false, ErrorHandler, null, out string filePath))
			{
				return null;
			}

			if (_files.ContainsKey(filePath))
			{
				return _files[filePath];
			}

			var file = ParseFile(filePath, body, ErrorHandler, globalScope);
			if (file == null)
				return null;

			_files[filePath] = file;

			return file;
		}

		private static bool ValidateFilePath(string dir, string filePath, bool isRel, IErrorHandler eh, (string file, ILocation loc)? from, out string path)
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
