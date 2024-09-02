using Frontend.Parsing.Entities;
using Frontend.Visitors;

namespace Frontend.Ast.Expressions
{
	public class AstUsingExpr : AstExpression
	{
		// public override bool IsPolymorphic => false;

		public AstIdExpr[] Path { get; }

		// when keyword 'as' is used
		public AstIdExpr AsWhat { get; }

		public AstUsingExpr(AstIdExpr[] path, AstIdExpr asWhat = null, ILocation Location = null) : base(Location)
		{
			this.Path = path;
			this.AsWhat = asWhat;
		}

		public override TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default)
			=> visitor.VisitUsingExpr(this, data);

		public override AstExpression Clone()
			=> CopyValuesTo(new AstUsingExpr(Path.Select(p => p.Clone() as AstIdExpr).ToArray()));
	}
}
