namespace HapetFrontend.Ast.Expressions
{
	public class AstIdExpr : AstExpression
	{
		public string Name { get; set; }

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
