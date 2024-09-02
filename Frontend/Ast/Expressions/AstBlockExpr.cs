using Frontend.Ast.Statements;
using Frontend.Parsing.Entities;
using Frontend.Scoping.Entities;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Expressions
{
	public class AstBlockExpr : AstNestedExpression, IBreakable
	{
		public List<AstStatement> Statements { get; set; }
		public AstIdExpr Label { get; set; }

		public List<AstStatement> Destructions { get; private set; } = null;

		public AstBlockExpr(List<AstStatement> statements, AstIdExpr label = null, ILocation Location = null) : base(Location: Location)
		{
			this.Statements = statements;
			this.Label = label;
		}

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitBlockExpr(this, data);

		public override AstExpression Clone()
			=> CopyValuesTo(new AstBlockExpr(
					Statements.Select(s => s.Clone()).ToList(),
					Label?.Clone() as AstIdExpr
				));

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
