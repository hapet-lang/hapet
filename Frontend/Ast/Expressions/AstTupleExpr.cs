using Frontend.Parsing.Entities;
using Frontend.Visitors;

namespace Frontend.Ast.Expressions
{
	public class AstTupleExpr : AstExpression
	{
		public override TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default) => visitor.VisitTupleExpr(this, data);

		public bool IsTypeExpr { get; set; } = false;
		public bool IsFullyNamed => Types.All(t => t.Name != null);
		public List<AstExpression> Values { get; set; }
		public List<AstParameter> Types { get; set; }

		public AstTupleExpr(List<AstParameter> values, ILocation Location)
			: base(Location)
		{
			this.Types = values;
			this.Values = Types.Select(t => t.TypeExpr).ToList();
		}
		public AstTupleExpr(List<AstExpression> values, ILocation Location)
			: base(Location)
		{
			this.Types = values.Select(v => new AstParameter(null, v, null, v.Location)).ToList();
			this.Values = Types.Select(t => t.TypeExpr).ToList();
		}

		public override AstExpression Clone()
			=> CopyValuesTo(new AstTupleExpr(Types.Select(v => v.Clone()).ToList(), Location));
	}
}
