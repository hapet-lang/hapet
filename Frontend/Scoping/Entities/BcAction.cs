namespace Frontend.Scoping.Entities
{
	public interface IBreakable
	{
		AstIdExpr Label { get; }
	}

	public interface IContinuable
	{
		AstIdExpr Label { get; }
	}

	internal class BcAction : IBreakable, IContinuable
	{
		public AstIdExpr Label => throw new NotImplementedException();
		public AstExpression Action { get; set; }

		public BcAction(AstExpression action)
		{
			this.Action = action;
		}
	}
}
