using Frontend.Parsing.Entities;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Statements
{
	public class AstExprStmt : AstStatement
	{
		public AstExpression Expr { get; set; }
		public List<AstStatement> Destructions { get; private set; } = null;

		[DebuggerStepThrough]
		public AstExprStmt(AstExpression expr, ILocation Location = null) : base(Location: Location)
		{
			this.Expr = expr;
		}

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitExprStmt(this, data);

		public override AstStatement Clone()
			=> CopyValuesTo(new AstExprStmt(Expr.Clone()));

		public override string ToString() => $"#expr {base.ToString()}";

		public void RemoveDestructions()
		{
			Destructions = null;
		}

		public void AddDestruction(AstStatement dest)
		{
			if (dest == null)
				return;
			if (Destructions == null)
				Destructions = new List<AstStatement>();
			Destructions.Add(dest);
		}
	}
}
