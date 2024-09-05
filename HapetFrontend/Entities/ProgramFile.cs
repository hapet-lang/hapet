using HapetFrontend.Ast;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Scoping;

namespace HapetFrontend.Entities
{
	public class ProgramFile
	{
		/// <summary>
		/// Filename without path parts
		/// </summary>
		public string Name { get; }
		/// <summary>
		/// To grab the text only once and store it here
		/// </summary>
		public string Text { get; }

		public Scope FileScope { get; }

		public List<AstStatement> Statements { get; } = new List<AstStatement>();
		public List<AstUsingStmt> Usings { get; set; } = new List<AstUsingStmt>();

		public ProgramFile(string name, string text, Scope scope)
		{
			this.Name = name;
			this.Text = text;
			FileScope = scope;
		}

		public override string ToString()
		{
			return $"ProgramFile: {Name}";
		}
	}
}
