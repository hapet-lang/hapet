using Frontend.Parsing.Entities;
using Frontend.Visitors;

namespace Frontend.Ast.Expressions
{
	public class AstArgument : AstExpression
	{
		public AstExpression Expr { get; set; }
		public AstIdExpr Name { get; set; }
		public int Index { get; set; } = -1;

		public bool IsDefaultArg { get; set; } = false;
		public bool IsConstArg { get; set; } = false;

		public AstArgument(AstExpression expr, AstIdExpr name = null, ILocation Location = null)
			: base(Location)
		{
			this.Expr = expr;
			this.Name = name;
		}

		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitArgumentExpr(this, data);

		public override AstExpression Clone()
			=> CopyValuesTo(new AstArgument(Expr.Clone(), Name?.Clone() as AstIdExpr));
	}
}
