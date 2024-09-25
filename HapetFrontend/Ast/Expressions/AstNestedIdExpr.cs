namespace HapetFrontend.Ast.Expressions
{
	public class AstNestedIdExpr : AstIdExpr
	{
		/// <summary>
		/// This is the left part of an id expr like the 'a.mm.anime.Test'
		/// where 'Test' would be the Name and 'a.mm.anime' would be the LeftPart 
		/// with its parsed names
		/// </summary>
		public AstNestedIdExpr LeftPart { get; set; }

		public AstNestedIdExpr(string name, AstNestedIdExpr leftPart, ILocation Location = null) : base(name, Location)
		{
			this.Name = name;
			this.LeftPart = leftPart;
		}
	}
}
