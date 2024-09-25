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

		public AstNestedIdExpr ToNested()
		{
			var nested = ToNestedInternal(Name, Location);
			return nested;

			AstNestedIdExpr ToNestedInternal(string name, ILocation loc)
			{
				if (name.Contains('.'))
				{
					// if contains type of instance name
					string[] ids = name.Split('.');
					string tp = string.Join('.', ids.Take(ids.Length - 1)); // take all except the last one
					string fc = ids[ids.Length - 1]; // take the last one
					return new AstNestedIdExpr(fc, ToNestedInternal(tp, loc), loc); // recursively
				}
				return new AstNestedIdExpr(name, null, loc);
			}
		}
	}
}
