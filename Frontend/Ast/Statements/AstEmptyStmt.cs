using Frontend.Parsing.Entities;
using Frontend.Visitors;

namespace Frontend.Ast.Statements
{
	public class AstEmptyStmt : AstStatement
	{
		public AstEmptyStmt(ILocation Location = null) : base(Location: Location) { }
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitEmptyStmt(this, data);
		public override AstStatement Clone() => CopyValuesTo(new AstEmptyStmt());
	}
}
