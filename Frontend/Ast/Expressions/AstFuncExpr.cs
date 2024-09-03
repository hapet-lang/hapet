using Frontend.Parsing.Entities;
using Frontend.Scoping;
using Frontend.Scoping.Entities;
using Frontend.Types;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Expressions
{
	public class AstFuncExpr : AstExpression, ITypedSymbol
	{
		public Scope ConstScope { get; set; }
		public Scope SubScope { get; set; }

		public string Name { get; set; }

		public List<AstParameter> Parameters { get; set; }
		public AstParameter ReturnTypeExpr { get; set; }

		public FunctionType FunctionType => Type as FunctionType;
		public HapetType ReturnType => ReturnTypeExpr?.Type ?? VoidType.Instance;

		public AstBlockExpr Body { get; private set; }

		public List<ILocation> InstantiatedAt { get; private set; } = null;
		public HashSet<AstFuncExpr> InstantiatedBy { get; private set; } = null;
		public AstClassTypeExpr Class { get; set; } = null;

		public ILocation ParameterLocation { get; internal set; }

		public bool IsGeneric { get; set; } = false; // TODO:
		public List<AstDirective> Directives { get; protected set; }

		public AstFuncExpr(List<AstParameter> parameters,
			AstParameter returns,
			AstBlockExpr body = null,
			List<AstDirective> Directives = null,
			ILocation Location = null,
			ILocation ParameterLocation = null)
			: base(Location)
		{
			this.Parameters = parameters;
			this.ReturnTypeExpr = returns;
			this.Body = body;
			this.ParameterLocation = ParameterLocation;
			this.Directives = Directives;
		}

		[DebuggerStepThrough]
		public override TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default)
			=> visitor.VisitFuncExpr(this, data);

		public override AstExpression Clone()
		{
			var copy = CopyValuesTo(new AstFuncExpr(
				Parameters.Select(p => p.Clone()).ToList(),
				ReturnTypeExpr?.Clone(),
				Body?.Clone() as AstBlockExpr,
				Directives?.Select(d => d.Clone())?.ToList(),
				ParameterLocation: ParameterLocation));
			copy.ConstScope = new Scope($"fn$", copy.Scope);
			copy.SubScope = new Scope($"fn {Name}", copy.ConstScope);
			copy.Name = Name;
			return copy;
		}

		public void AddInstantiatedAt(ILocation loc, AstFuncExpr func)
		{
			if (InstantiatedAt == null)
			{
				InstantiatedAt = new List<ILocation>();
				InstantiatedBy = new HashSet<AstFuncExpr>();
			}

			InstantiatedAt.Add(loc);

			if (func != null)
				InstantiatedBy.Add(func);
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

		public override string ToString()
		{
			return Accept(new AnalysedAstPrinter());
		}
	}
}
