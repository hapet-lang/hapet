using Frontend.Parsing.Entities;
using Frontend.Visitors;

namespace Frontend.Ast.Expressions
{
	public class AstRangeExpr : AstExpression
	{
		public AstExpression From { get; set; }
		public AstExpression To { get; set; }
		public bool Inclusive { get; set; }

		public AstRangeExpr(AstExpression from, AstExpression to, bool inclusive, ILocation Location = null) : base(Location: Location)
		{
			this.From = from;
			this.To = to;
			this.Inclusive = inclusive;
		}

		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitRangeExpr(this, data);

		public override AstExpression Clone()
			=> CopyValuesTo(new AstRangeExpr(
				From?.Clone(),
				To?.Clone(),
				Inclusive));
	}
}
