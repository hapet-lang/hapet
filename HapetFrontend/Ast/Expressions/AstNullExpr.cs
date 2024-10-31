using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
	public class AstNullExpr : AstExpression
	{
		/// <summary>
		/// The target type of null
		/// </summary>
		public HapetType Target { get; set; }

		[DebuggerStepThrough]
		public AstNullExpr(HapetType target, ILocation Location = null) : base(Location)
		{
			Target = target;
		}
	}
}
