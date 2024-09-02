using Frontend.Ast.Expressions;
using Frontend.Parsing.Entities;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Declarations
{
	public class AstConstantDeclaration : AstDeclaration
	{
		public AstExpression Pattern { get; set; }
		public AstExpression TypeExpr { get; set; }
		public AstExpression Initializer { get; set; }

		public object Value { get; set; }

		public AstConstantDeclaration(
			AstExpression pattern,
			AstExpression typeExpr,
			AstExpression init,
			string documentation,
			List<AstDirective> directives,
			ILocation Location = null)
			: base(
				pattern is AstIdExpr ? (pattern as AstIdExpr) : (new AstIdExpr(pattern.ToString(), false, pattern.Location)),
				documentation,
				directives,
				Location)
		{
			this.Pattern = pattern;
			this.TypeExpr = typeExpr;
			this.Initializer = init;
		}

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default) => visitor.VisitConstantDeclaration(this, data);

		public override AstStatement Clone()
			=> CopyValuesTo(new AstConstantDeclaration(
				Pattern.Clone(),
				TypeExpr?.Clone(),
				Initializer.Clone(),
				Documentation,
				Directives?.Select(d => d.Clone()).ToList()));
	}
}
