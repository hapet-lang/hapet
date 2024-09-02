using Frontend.Parsing.Entities;
using Frontend.Visitors;

namespace Frontend.Ast.Expressions
{
	public class AstUsingExpr : AstExpression
	{
		// public override bool IsPolymorphic => false;

		public AstIdExpr[] Path { get; }

		public AstUsingExpr(AstIdExpr[] path, ILocation Location = null) : base(Location)
		{
			this.Path = path;
		}

		public override TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default)
			=> visitor.VisitUsingExpr(this, data);

		public override AstExpression Clone()
			=> CopyValuesTo(new AstUsingExpr(Path.Select(p => p.Clone() as AstIdExpr).ToArray()));
	}
}
