using Frontend.Parsing.Entities;
using Frontend.Types;
using Frontend.Visitors;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Frontend.Ast.Expressions
{
	public class AstEnumTypeExpr : AstExpression
	{
		//public string Name { get; set; } = "#anonymous";
		//public List<AstParameter> Parameters { get; set; }
		//public List<AstDecl> Declarations { get; }
		//public List<AstEnumMemberNew> Members { get; set; }

		//public EnumType EnumType => Value as EnumType;
		//public CheezType TagType { get; set; }

		//public AstEnumTypeExpr Template { get; set; } = null;

		//public Scope SubScope { get; set; }

		//public bool IsGeneric { get; set; }
		//public override bool IsPolymorphic => IsGeneric;

		//public bool IsPolyInstance { get; set; }

		//public List<AstEnumTypeExpr> PolymorphicInstances { get; } = new List<AstEnumTypeExpr>();

		//public List<TraitType> Traits { get; } = new List<TraitType>();
		//public List<AstDirective> Directives { get; protected set; }

		//public bool MembersComputed { get; set; } = false;

		//public bool IsReprC { get; set; }
		//public bool IsFlags { get; set; }
		//public bool Untagged { get; internal set; } = false;

		//public AstEnumTypeExpr(List<AstParameter> param, List<AstDecl> declarations, List<AstDirective> Directives = null, ILocation Location = null)
		//	: base(Location)
		//{
		//	this.Parameters = param ?? new List<AstParameter>();
		//	this.Declarations = declarations;
		//	this.IsGeneric = Parameters.Count > 0;
		//	this.Directives = Directives;
		//}

		//[DebuggerStepThrough]
		//public override TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default) => visitor.VisitEnumTypeExpr(this, data);

		//public override AstExpression Clone() => CopyValuesTo(
		//	new AstEnumTypeExpr(
		//		Parameters.Select(p => p.Clone()).ToList(),
		//		Declarations.Select(m => m.Clone() as AstDecl).ToList(),
		//		Directives.Select(d => d.Clone()).ToList())
		//	{
		//		Untagged = this.Untagged
		//	});

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
