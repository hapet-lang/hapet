using Frontend.Parsing.Entities;
using Frontend.Types;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Expressions
{
	public class AstBinaryExpr : AstExpression
	{
		public string Operator { get; set; }
		public AstExpression Left { get; set; }
		public AstExpression Right { get; set; }

		public IBinaryOperator ActualOperator { get; set; }

		[DebuggerStepThrough]
		public AstBinaryExpr(string op, AstExpression lhs, AstExpression rhs, ILocation Location = null) : base(Location)
		{
			Operator = op;
			Left = lhs;
			Right = rhs;
		}

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitBinaryExpr(this, data);

		[DebuggerStepThrough]
		public override AstExpression Clone()
			=> CopyValuesTo(new AstBinaryExpr(Operator, Left.Clone(), Right.Clone()));
	}
}
