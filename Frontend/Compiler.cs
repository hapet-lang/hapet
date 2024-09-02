using Frontend.Ast;
using Frontend.Parsing.Entities;
using Frontend.Scoping;

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

	public class Compiler : ITextProvider
	{
		public string GetText(ILocation location)
		{
			throw new NotImplementedException();
		}
	}
}
