using Frontend.Parsing.Entities;
using Frontend.Scoping;
using Frontend.Types;
using Frontend.Visitors;

namespace Frontend.Ast.Expressions
{
	public class AstClassTypeExpr : AstExpression
	{
		public string Name { get; set; } = "#anonymous";

		/// <summary>
		/// Generic shite
		/// </summary>
		public List<AstParameter> Parameters { get; set; }

		public List<AstDeclaration> Declarations { get; }

		public bool IsPolyInstance { get; set; }

		public List<AstClassTypeExpr> GenericInstances { get; } = new List<AstClassTypeExpr>();
		public AstClassTypeExpr Template { get; set; } = null;

		public Scope SubScope { get; set; }

		public bool IsGeneric { get; set; }
		public List<AstDirective> Directives { get; protected set; }

		public ClassType ClassType => Value as ClassType;

		public AstClassTypeExpr(
			List<AstParameter> parameters,
			List<AstDeclaration> declarations,
			List<AstDirective> Directives = null,
			ILocation Location = null)
			: base(Location: Location)
		{
			this.Parameters = parameters;
			this.Declarations = declarations;
			this.Directives = Directives;
			this.IsGeneric = Parameters?.Count > 0;
		}

		public override TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default) => visitor.VisitClassTypeExpr(this, data);

		public override AstExpression Clone() => CopyValuesTo(
			new AstClassTypeExpr(
				Parameters.Select(p => p.Clone()).ToList(),
				Declarations.Select(d => d.Clone() as AstDeclaration).ToList(),
				Directives.Select(d => d.Clone()).ToList()));

		public bool HasDirective(string name) => Directives?.Find(d => d.Name.Name == name) != null;

		public AstDirective GetDirective(string name)
		{
			return Directives?.FirstOrDefault(d => d.Name.Name == name);
		}

		public bool TryGetDirective(string name, out AstDirective dir)
		{
			dir = Directives?.FirstOrDefault(d => d.Name.Name == name);
			return dir != null;
		}
	}
}
