using Frontend.Parsing.Entities;
using Frontend.Types;
using System.Diagnostics;

namespace Frontend.Scoping.Entities
{
	public class Using : ITypedSymbol
	{
		public HapetType Type => Expr.Type;
		public string Name => null;

		public AstExpression Expr { get; }

		public ILocation Location { get; set; }
		public bool Replace { get; set; } = false;


		[DebuggerStepThrough]
		public Using(AstExpression expr, bool replace)
		{
			this.Location = expr.Location;
			this.Expr = expr;
			this.Replace = replace;
		}

		[DebuggerStepThrough]
		public override string ToString()
		{
			return $"using {Expr}";
		}
	}
}
