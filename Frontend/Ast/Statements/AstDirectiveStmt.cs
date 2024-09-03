using Frontend.Parsing.Entities;
using Frontend.Visitors;

namespace Frontend.Ast.Statements
{
	public class AstDirectiveStmt : AstStatement
	{
		public AstDirective Directive { get; }

		public AstDirectiveStmt(AstDirective Directive, ILocation Location = null) : base(Location: Location)
		{
			this.Directive = Directive;
		}

		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitDirectiveStmt(this, data);

		public override AstStatement Clone() => CopyValuesTo(new AstDirectiveStmt(Directive));
	}
}
