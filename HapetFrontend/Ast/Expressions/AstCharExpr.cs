using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
	public class AstCharExpr : AstExpression
	{
		public char CharValue { get; set; }
		public string RawValue { get; set; }

		[DebuggerStepThrough]
		public AstCharExpr(string rawValue, ILocation Location = null) : base(Location)
		{
			this.RawValue = rawValue;
		}
	}
}
