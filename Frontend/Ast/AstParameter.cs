using Frontend.Ast.Expressions;
using Frontend.Parsing.Entities;
using Frontend.Scoping;
using Frontend.Scoping.Entities;
using Frontend.Types;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast
{
	public class AstParameter : ITypedSymbol, ILocation
	{
		public ILocation Location { get; private set; }
		public TokenLocation Beginning => Location?.Beginning;
		public TokenLocation Ending => Location?.Ending;

		public AstIdExpr Name { get; set; }

		string ISymbol.Name => Name?.Name;
		public HapetType Type { get; set; }
		public AstExpression TypeExpr { get; set; }
		public AstExpression DefaultValue { get; set; }

		public Scope Scope { get; set; }

		public ISymbol Symbol { get; set; } = null;

		public object Value { get; set; }

		public bool IsReturnParam { get; set; } = false;

		public AstExpression ContainingFunction { get; set; }

		public AstParameter(AstIdExpr name, AstExpression typeExpr, AstExpression defaultValue, ILocation Location = null)
		{
			this.Location = Location;
			this.Name = name;
			this.TypeExpr = typeExpr;
			this.DefaultValue = defaultValue;
		}

		public AstParameter Clone()
		{
			return new AstParameter(Name?.Clone() as AstIdExpr, TypeExpr?.Clone(), DefaultValue?.Clone(), Location);
		}

		[DebuggerStepThrough]
		public TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default) => visitor.VisitParameter(this, data);
	}
}
