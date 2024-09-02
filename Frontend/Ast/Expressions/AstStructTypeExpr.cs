using Frontend.Parsing.Entities;
using Frontend.Types;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Expressions
{
	public class AstStructTypeExpr : AstExpression
	{
		// TODO: impl
		//public string Name { get; set; } = "#anonymous";
		//public List<AstParameter> Parameters { get; set; }
		//public AstExpression TraitExpr { get; set; }
		//public List<AstDecl> Declarations { get; }
		//public List<AstStructMemberNew> Members { get; set; }
		//public IReadOnlyList<AstStructMemberNew> PublicMembers => Members.Where(m => m.IsPublic).ToList();

		//public StructType StructType => Value as StructType;
		//public TraitType BaseTrait => TraitExpr?.Value as TraitType;

		//public AstStructTypeExpr Template { get; set; } = null;

		//public Scope SubScope { get; set; }

		//public bool IsGeneric { get; set; }
		//public override bool IsPolymorphic => IsGeneric;

		//public bool IsPolyInstance { get; set; }

		//public List<AstStructTypeExpr> PolymorphicInstances { get; } = new List<AstStructTypeExpr>();

		//public List<TraitType> Traits { get; } = new List<TraitType>();
		//public List<AstDirective> Directives { get; protected set; }

		//public bool Extendable { get; set; }
		//public StructType Extends { get; set; }

		//public bool TypesComputed { get; set; }
		//public bool InitializersComputed { get; set; }

		//public AstStructTypeExpr(List<AstParameter> param, AstExpression traitExpr, List<AstDecl> declarations, List<AstDirective> Directives = null, ILocation Location = null)
		//	: base(Location)
		//{
		//	this.Parameters = param ?? new List<AstParameter>();
		//	this.TraitExpr = traitExpr;
		//	this.Declarations = declarations;
		//	this.IsGeneric = Parameters.Count > 0;
		//	this.Directives = Directives;
		//}

		//[DebuggerStepThrough]
		//public override TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default) => visitor.VisitStructTypeExpr(this, data);

		//public override AstExpression Clone() => CopyValuesTo(
		//	new AstStructTypeExpr(
		//		Parameters.Select(p => p.Clone()).ToList(),
		//		TraitExpr?.Clone(),
		//		Declarations.Select(m => m.Clone() as AstDecl).ToList(),
		//		Directives.Select(d => d.Clone()).ToList()));

		//public bool HasDirective(string name) => Directives?.Find(d => d.Name.Name == name) != null;

		//public AstDirective GetDirective(string name)
		//{
		//	return Directives?.FirstOrDefault(d => d.Name.Name == name);
		//}

		//public bool TryGetDirective(string name, out AstDirective dir)
		//{
		//	dir = Directives?.FirstOrDefault(d => d.Name.Name == name);
		//	return dir != null;
		//}
		public override TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default)
		{
			throw new NotImplementedException();
		}

		public override AstExpression Clone()
		{
			throw new NotImplementedException();
		}
	}
}
