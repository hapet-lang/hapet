using Frontend.Parsing.Entities;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Expressions
{
	public class AstDereferenceExpr : AstExpression
	{
		public AstExpression SubExpression { get; set; }

		public bool Reference { get; set; } = false;

		[DebuggerStepThrough]
		public AstDereferenceExpr(AstExpression sub, ILocation Location = null) : base(Location)
		{
			SubExpression = sub;
		}

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitDerefExpr(this, data);

		[DebuggerStepThrough]
		public override AstExpression Clone()
			=> CopyValuesTo(new AstDereferenceExpr(SubExpression.Clone())
			{
				Reference = Reference
			});
	}
}
