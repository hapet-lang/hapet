using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
	public class AstStringExpr : AstExpression
	{
		public string StringValue => (string)OutValue;
		public string Suffix { get; set; }

		[DebuggerStepThrough]
		public AstStringExpr(string value, string suffix = null, ILocation Location = null) : base(Location)
		{
			this.OutValue = value;
			this.Suffix = suffix;
			OutType = StringType.Instance;
		}
	}
}
