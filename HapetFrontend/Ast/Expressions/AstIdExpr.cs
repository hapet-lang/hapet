using HapetFrontend.Scoping;

namespace HapetFrontend.Ast.Expressions
{
	public class AstIdExpr : AstExpression
	{
		public string Name { get; set; }

		/// <summary>
		/// Getting symbol of itself
		/// </summary>
		public ISymbol FindSymbol 
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

		public AstIdExpr GetCopy(string name = "")
		{
			string newName = string.IsNullOrWhiteSpace(name) ? Name : name;
			var newId = new AstIdExpr(newName, Location)
			{
				Parent = this.Parent,
				Scope = this.Scope,
				IsCompileTimeValue = this.IsCompileTimeValue,
				OutType = this.OutType,
				OutValue = this.OutValue,
				SourceFile = this.SourceFile,
			};
			return newId;
		}
	}
}
