using Frontend.Parsing.Entities;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Expressions
{
	public class AstArrayAccessExpr : AstExpression
	{
		public AstExpression SubExpression { get; set; }
		public List<AstExpression> Arguments { get; set; }

		[DebuggerStepThrough]
		public AstArrayAccessExpr(AstExpression sub, List<AstExpression> args, ILocation Location = null) : base(Location)
		{
			SubExpression = sub;
			Arguments = args;
		}

		[DebuggerStepThrough]
		public AstArrayAccessExpr(AstExpression sub, AstExpression arg, ILocation Location = null) : base(Location)
		{
			SubExpression = sub;
			Arguments = new List<AstExpression> { arg };
		}

		[DebuggerStepThrough]
		public override TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default) => visitor.VisitArrayAccessExpr(this, data);

		[DebuggerStepThrough]
		public override AstExpression Clone() =>
			CopyValuesTo(new AstArrayAccessExpr(
				SubExpression.Clone(),
				Arguments.Select(a => a.Clone()).ToList()));
	}
}
