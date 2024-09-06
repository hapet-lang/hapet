using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Parsing;
using HapetFrontend.Parsing.PostPrepare;
using HapetFrontend.Scoping;
using System.Collections.ObjectModel;

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

			_globalScope = new Scope("global_scope_of_assembly");
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

			return file;
		}

		private ProgramFile ParseFile(string fileName, IErrorHandler eh)
		{
			var lexer = Lexer.FromFile(fileName, eh);

			if (lexer == null)
				return null;

			var parser = new Parser(lexer, eh);

			var fileScope = new Scope($"{Path.GetFileNameWithoutExtension(fileName)}_scope", _globalScope);
			var file = new ProgramFile(fileName, lexer.Text, fileScope);

			_files[fileName] = file;

			while (true)
			{
				var s = parser.ParseStatement();
				if (s == null)
					break;

				HandleStatement(s);
			}

			return file;

			void HandleStatement(AstStatement s)
			{
				s.Scope = file.FileScope;
				if (s is AstEnumDecl ||
					s is AstStructDecl ||
					s is AstClassDecl ||
					s is AstUsingStmt)
				{
					s.SourceFile = file;
					file.Statements.Add(s);
				}
				else if (s != null)
				{
					eh.ReportError(lexer.Text, s, "This type of statement is not allowed in global scope");
				}
			}
		}

		public ProgramFile GetFile(string v)
		{
			var normalizedPath = Path.GetFullPath(v).PathNormalize();
			if (!_files.ContainsKey(normalizedPath))
				return null;
			return _files[normalizedPath];
		}

		public ReadOnlyDictionary<string, ProgramFile> GetFiles()
		{
			return new ReadOnlyDictionary<string, ProgramFile>(_files);
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
