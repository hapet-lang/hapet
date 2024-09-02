using Frontend.Parsing.Entities;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Expressions
{
	public class AstEmptyExpr : AstExpression
	{

		[DebuggerStepThrough]
		public AstEmptyExpr(ILocation Location = null) : base(Location)
		{ }

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default(D)) => visitor.VisitEmptyExpr(this, data);

		[DebuggerStepThrough]
		public override AstExpression Clone()
			=> CopyValuesTo(new AstEmptyExpr());
	}
}
