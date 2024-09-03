using Frontend.Parsing.Entities;
using Frontend.Types;
using Frontend.Visitors;

namespace Frontend.Ast.Expressions
{
	public class AstLambdaExpr : AstExpression
	{
		public List<AstParameter> Parameters { get; set; }
		public AstExpression ReturnTypeExpr { get; set; }
		public AstExpression Body { get; set; }

		public FunctionType FunctionType => Type as FunctionType;

		public AstLambdaExpr(List<AstParameter> parameters, AstExpression body, AstExpression retType, ILocation location = null)
			: base(location)
		{
			this.Parameters = parameters;
			this.ReturnTypeExpr = retType;
			this.Body = body;
		}

		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default)
			=> visitor.VisitLambdaExpr(this, data);

		public override AstExpression Clone()
			=> CopyValuesTo(new AstLambdaExpr(Parameters.Select(p => p.Clone()).ToList(), Body.Clone(), ReturnTypeExpr?.Clone()));
	}
}
