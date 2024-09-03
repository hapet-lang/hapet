using Frontend.Parsing.Entities;
using Frontend.Visitors;
using System.Xml.Linq;

namespace Frontend.Ast.Expressions
{
	public class AstTypeWithNameExpr : AstExpression
	{
		public override TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default) { return default; } // do nothing

		public (AstExpression tp, AstExpression name) TypeAndValue { get; set; }

		public AstTypeWithNameExpr(AstExpression type, AstExpression name, ILocation Location)
			: base(Location)
		{
			TypeAndValue = (type, name);
		}

		public override AstExpression Clone()
			=> CopyValuesTo(new AstTypeWithNameExpr(TypeAndValue.tp, TypeAndValue.name, Location));
	}
}
