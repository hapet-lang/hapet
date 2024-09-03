using Frontend.Parsing.Entities;
using Frontend.Types;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Expressions
{
	public class AstCallExpr : AstExpression
	{
		public AstExpression FunctionExpr { get; set; }
		public List<AstArgument> Arguments { get; set; }

		public AstFuncExpr Declaration { get; internal set; }
		public FunctionType FunctionType { get; set; } = null;

		[DebuggerStepThrough]
		public AstCallExpr(AstExpression func, List<AstArgument> args, ILocation Location = null) : base(Location)
		{
			FunctionExpr = func;
			Arguments = args;
		}

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitCallExpr(this, data);

		[DebuggerStepThrough]
		public override AstExpression Clone()
			=> CopyValuesTo(new AstCallExpr(FunctionExpr.Clone(), Arguments.Select(a => a.Clone() as AstArgument).ToList()));
	}
}
