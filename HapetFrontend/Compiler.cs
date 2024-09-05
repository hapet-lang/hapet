using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Scoping;

namespace HapetFrontend
{
	public class Compiler : ITextOnLocationProvider
	{
		/// <summary>
		/// Path and file
		/// </summary>
		private Dictionary<string, ProgramFile> _files = new Dictionary<string, ProgramFile>();

		public IErrorHandler ErrorHandler { get; }

		private Scope _globalScope = null;

		public Compiler(IErrorHandler errorHandler)
		{
			ErrorHandler = errorHandler;
			ErrorHandler.TextProvider = this;

			// TODO: do i need it?
			// string exePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "libraries");

			_globalScope = new Scope("global_scope");
			_globalScope.DefineBuiltInTypes();
			_globalScope.DefineBuiltInOperators();
		}

		public ProgramFile AddFile(string fileName)
		{
			if (!CompilerUtils.ValidateFilePath("", fileName, false, ErrorHandler, null, out string filePath))
			{
				return null;
			}

			if (_files.ContainsKey(filePath))
			{
				return _files[filePath];
			}

			var file = ParseFile(filePath, ErrorHandler);
			if (file == null)
				return null;

			_files[filePath] = file;
			return file;
		}

		public ProgramFile GetFile(string v)
		{
			var normalizedPath = Path.GetFullPath(v).PathNormalize();
			if (!_files.ContainsKey(normalizedPath))
				return null;
			return _files[normalizedPath];
		}

		public string GetText(ILocation location)
		{
			if (location is null)
				throw new ArgumentNullException(nameof(location));

			var normalizedPath = Path.GetFullPath(location.Beginning.File).PathNormalize();

			// files
			if (_files.TryGetValue(normalizedPath, out var f))
				return f.Text;

			return null;
		}
	}
}
