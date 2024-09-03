using Frontend.Ast.Expressions;
using Frontend.Parsing.Entities;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast.Declarations
{
	public class AstVariableDecl : AstDeclaration
	{
		public AstExpression Pattern { get; set; }
		public AstExpression TypeExpr { get; set; }
		public AstExpression Initializer { get; set; }

		public AstFuncExpr ContainingFunction { get; set; }

		public AstVariableDecl(
			AstExpression pattern,
			AstExpression typeExpr,
			AstExpression init,
			string documentation = null,
			List<AstDirective> Directives = null, ILocation Location = null)
			: base(pattern is AstIdExpr ? (pattern as AstIdExpr) : (new AstIdExpr(pattern.ToString(), false, pattern.Location)), documentation, Directives, Location)
		{
			this.Pattern = pattern;
			this.TypeExpr = typeExpr;
			this.Initializer = init;
		}

		[DebuggerStepThrough]
		public override T Accept<T, D>(IVisitor<T, D> visitor, D data = default(D)) => visitor.VisitVariableDecl(this, data);

		public override AstStatement Clone()
			=> CopyValuesTo(new AstVariableDecl(
				Pattern.Clone(),
				TypeExpr?.Clone(),
				Initializer?.Clone(),
				Documentation));
	}
}
