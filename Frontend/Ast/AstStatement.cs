using Frontend.Parsing.Entities;
using Frontend.Scoping;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast
{
	public enum StmtFlags
	{
		GlobalScope,
		Returns,
		IsLastStatementInBlock,
		NoDefaultInitializer,
		MembersComputed,
		ExcludeFromVtable,
		IsMacroFunction,
		IsForExtension,
		IsCopy,
		Breaks,
		IsLocal
	}

	public abstract class AstStatement : IVisitorAcceptor, ILocation, IAstNode
	{
		protected int _flags { get; private set; } = 0;

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

		public void SetFlag(StmtFlags f, bool b = true)
		{
			if (b)
				_flags |= 1 << (int)f;
			else
				_flags &= ~(1 << (int)f);
		}

		public bool GetFlag(StmtFlags f) => (_flags & (1 << (int)f)) != 0;

		public int GetFlags() => _flags;
		public void SetFlags(int flags)
		{
			_flags = flags;
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

	public class AstAssignment : AstStatement
	{
		public AstExpression Pattern { get; set; }
		public AstExpression Value { get; set; }
		public string Operator { get; set; }

		public List<AstAssignment> SubAssignments { get; set; }
		public bool OnlyGenerateValue { get; internal set; } = false;

		public List<AstStatement> Destructions { get; private set; } = null;

		public AstAssignment(AstExpression target, AstExpression value, string op = null, ILocation Location = null)
			: base(Location: Location)
		{
			this.Pattern = target;
			this.Value = value;
			this.Operator = op;
		}

		public void AddSubAssignment(AstAssignment ass)
		{
			if (SubAssignments == null) SubAssignments = new List<AstAssignment>();
			SubAssignments.Add(ass);
		}

		public void AddDestruction(AstStatement dest)
		{
			if (dest == null)
				return;
			if (Destructions == null)
				Destructions = new List<AstStatement>();
			Destructions.Add(dest);
		}

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitAssignmentStmt(this, data);

		public override AstStatement Clone()
			=> CopyValuesTo(new AstAssignment(Pattern.Clone(), Value.Clone(), Operator));
	}
}
