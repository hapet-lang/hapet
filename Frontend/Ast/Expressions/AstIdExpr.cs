using Frontend.Parsing.Entities;
using Frontend.Scoping.Entities;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Expressions
{
	public class AstIdExpr : AstExpression
	{
		public string Name { get; set; }
		public ISymbol Symbol { get; set; }

		public bool IsGeneric { get; private set; }
		/// <summary>
		/// used only if AstIdExpr is a type
		/// </summary>
		public bool IsArray { get; set; } 

		[DebuggerStepThrough]
		public AstIdExpr(string name, bool isPolyTypeExpr, ILocation Location = null) : base(Location)
		{
			this.Name = name;
			this.IsGeneric = isPolyTypeExpr;
		}

		public void SetIsPolymorphic(bool b)
		{
			IsGeneric = b;
		}

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitIdExpr(this, data);

		[DebuggerStepThrough]
		public override AstExpression Clone() => CopyValuesTo(new AstIdExpr(Name, IsGeneric));
	}
}
