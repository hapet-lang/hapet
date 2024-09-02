using Frontend.Parsing.Entities;
using Frontend.Scoping;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast
{
	public abstract class AstStatement : IVisitorAcceptor, ILocation, IAstNode
	{
		public ILocation Location { get; private set; }
		public TokenLocation Beginning => Location?.Beginning;
		public TokenLocation Ending => Location?.Ending;

		public PtFile SourceFile { get; set; }

		public Scope Scope { get; set; }
		public List<AstDirective> Directives { get; set; }

		public IAstNode Parent { get; set; }

		public int Position { get; set; } = 0;

		public AstStatement(List<AstDirective> dirs = null, ILocation Location = null)
		{
			this.Directives = dirs ?? new List<AstDirective>();
			this.Location = Location;
		}

		public bool HasDirective(string name) => Directives.Find(d => d.Name.Name == name) != null;

		public AstDirective GetDirective(string name)
		{
			return Directives.FirstOrDefault(d => d.Name.Name == name);
		}

		public bool TryGetDirective(string name, out AstDirective dir)
		{
			dir = Directives.FirstOrDefault(d => d.Name.Name == name);
			return dir != null;
		}

		[DebuggerStepThrough]
		public abstract T Accept<T, D>(IVisitor<T, D> visitor, D data = default);

		public abstract AstStatement Clone();

		protected T CopyValuesTo<T>(T to)
			where T : AstStatement
		{
			to.Location = this.Location;
			to.Parent = this.Parent;
			to.Scope = this.Scope;
			to.Directives = this.Directives;
			to.SourceFile = this.SourceFile;

			return to;
		}

		public override string ToString()
		{
			return string.Empty;
		}
	}
}
