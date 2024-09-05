using HapetFrontend.Ast.Expressions;
using HapetFrontend.Scoping;

namespace HapetFrontend.Ast
{
	public abstract class AstDeclaration : AstStatement
	{
		public AstIdExpr Type { get; set; }
		public AstIdExpr Name { get; set; }

		public string Documentation { get; set; }

		/// <summary>
		/// In which scope it could be accessable
		/// </summary>
		public Scope Scope { get; set; }

		public AstDeclaration(AstIdExpr name, string doc, ILocation Location = null) : base(Location)
		{
			this.Name = name;
			this.Documentation = doc;
		}
	}
}
