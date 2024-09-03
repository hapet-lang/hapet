using Frontend.Parsing.Entities;
using Frontend.Types;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Expressions
{
	public class AstUnaryExpr : AstExpression
	{
		public string Operator { get; set; }
		public AstExpression SubExpr { get; set; }

		public IUnaryOperator ActualOperator { get; internal set; } = null;

		[DebuggerStepThrough]
		public AstUnaryExpr(string op, AstExpression sub, ILocation Location = null) : base(Location)
		{
			Operator = op;
			SubExpr = sub;
		}

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitUnaryExpr(this, data);

		[DebuggerStepThrough]
		public override AstExpression Clone()
			=> CopyValuesTo(new AstUnaryExpr(Operator, SubExpr.Clone()));
	}
}
