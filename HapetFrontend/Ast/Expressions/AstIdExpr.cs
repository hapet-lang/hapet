using HapetFrontend.Scoping;

namespace HapetFrontend.Ast.Expressions
{
	public class AstIdExpr : AstExpression
	{
		public string Name { get; set; }

		/// <summary>
		/// Getting symbol of itself
		/// </summary>
		public ISymbol Symbol 
		{
			get
			{
				return Scope.GetSymbol(Name);
			} 
		}

		public AstIdExpr(string name, ILocation Location = null) : base(Location)
		{
			this.Name = name;
		}

		public override string ToString()
		{
			return Name;
		}
	}
}
