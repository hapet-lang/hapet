using Frontend.Ast;
using Frontend.Errors;
using Frontend.Parsing.Entities;
using Frontend.Scoping;
using Microsoft.VisualBasic;

namespace Frontend
{
	public class PtFile
	{
		public string Name { get; }
		public string Text { get; }

		public Scope FileScope { get; }
		public Scope ExportScope { get; }

		public List<AstStatement> Statements { get; } = new List<AstStatement>();

		public List<string> Libraries { get; set; } = new List<string>();

		public PtFile(string name, string raw, Scope scope)
		{
			this.Name = name;
			this.Text = raw;
			FileScope = scope;
			ExportScope = new Scope("export");
		}

		public override string ToString()
		{
			return $"PtFile: {Name}";
		}
	}

	public interface ITextProvider
	{
		string GetText(ILocation location);
	}

	public partial class Compiler : ITextProvider
	{
		public IErrorHandler ErrorHandler { get; private set; }

		private Scope _globalScope = null;

		public Compiler(IErrorHandler errorHandler)
		{
			ErrorHandler = errorHandler;

			_globalScope = new Scope("global_scope");
			_globalScope.DefineBuiltInTypes();
			_globalScope.DefineBuiltInOperators();
		}

		public string GetText(ILocation location)
		{
			if (location is null)
				throw new ArgumentNullException(nameof(location));

			var normalizedPath = Path.GetFullPath(location.Beginning.File).PathNormalize();

			// files
			if (_files.TryGetValue(normalizedPath, out var f))
				return f.Text;
			if (_loadingFiles.TryGetValue(normalizedPath, out var f2))
				return f2.Text;

			// strings
			if (_strings.TryGetValue(location.Beginning.File, out var f3))
				return f3;

			return null;
		}
	}
}
