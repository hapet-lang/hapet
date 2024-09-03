using Frontend.Parsing.Entities;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Expressions
{
	public class AstAddressOfExpr : AstExpression
	{
		public AstExpression SubExpression { get; set; }
		public bool Reference { get; set; } = false;

		[DebuggerStepThrough]
		public AstAddressOfExpr(AstExpression sub, bool reference, ILocation Location = null) : base(Location)
		{
			SubExpression = sub;
			Reference = reference;
		}

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitAddressOfExpr(this, data);

		[DebuggerStepThrough]
		public override AstExpression Clone()
			=> CopyValuesTo(new AstAddressOfExpr(SubExpression.Clone(), Reference));
	}
}
