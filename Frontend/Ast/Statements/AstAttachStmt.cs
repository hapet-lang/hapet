using Frontend.Parsing.Entities;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Statements
{
	public class AstAttachStmt : AstStatement
	{
		public AstExpression Value { get; set; }

		[DebuggerStepThrough]
		public AstAttachStmt(AstExpression expr, List<AstDirective> Directives = null, ILocation Location = null)
			: base(Directives, Location)
		{
			Value = expr;
		}

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitAttachStmt(this, data);

		public override AstStatement Clone()
			=> CopyValuesTo(new AstAttachStmt(Value.Clone()));
	}
}
