using Frontend.Parsing.Entities;
using Frontend.Visitors;

namespace Frontend.Ast.Expressions
{
	public class AstClassTypeExpr : AstExpression
	{
		// TODO: impl
		//public string Name { get; set; } = "#anonymous";

		//public List<AstParameter> Parameters { get; set; }

		//public List<AstDeclaration> Declarations { get; }
		//public List<AstFuncExpr> Functions { get; } = new List<AstFuncExpr>();
		//public List<AstTraitMember> Members { get; } = new List<AstTraitMember>();

		//public Dictionary<CheezType, AstImplBlock> Implementations { get; } = new Dictionary<CheezType, AstImplBlock>();

		//public bool IsPolyInstance { get; set; }

		//public List<AstTraitTypeExpr> PolymorphicInstances { get; } = new List<AstTraitTypeExpr>();
		//public AstTraitTypeExpr Template { get; set; } = null;

		//public Scope SubScope { get; set; }

		//public bool IsGeneric { get; set; }
		//public override bool IsPolymorphic => IsGeneric;
		//public List<AstDirective> Directives { get; protected set; }

		//public TraitType TraitType => Value as TraitType;

		//// flags
		//public bool MembersComputed { get; set; }

		//public AstTraitTypeExpr(
		//	List<AstParameter> parameters,
		//	List<AstDecl> declarations,
		//	List<AstDirective> Directives = null,
		//	ILocation Location = null)
		//	: base(Location: Location)
		//{
		//	this.Parameters = parameters;
		//	this.Declarations = declarations;
		//	this.Directives = Directives;
		//	this.IsGeneric = Parameters?.Count > 0;
		//}

		//public override TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default) => visitor.VisitTraitTypeExpr(this, data);

		//public override AstExpression Clone() => CopyValuesTo(
		//	new AstTraitTypeExpr(
		//		Parameters.Select(p => p.Clone()).ToList(),
		//		Declarations.Select(d => d.Clone() as AstDecl).ToList(),
		//		Directives.Select(d => d.Clone()).ToList()));

		//public AstImplBlock FindMatchingImplementation(CheezType from)
		//{
		//	foreach (var kv in Implementations)
		//	{
		//		var type = kv.Key;
		//		var impl = kv.Value;
		//		if (CheezType.TypesMatch(type, from))
		//		{
		//			return impl;
		//		}
		//	}

		//	return null;
		//}

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
