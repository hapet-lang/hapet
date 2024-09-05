namespace HapetFrontend.Ast
{
	public interface IAstNode
	{
		IAstNode Parent { get; }
	}

	public abstract class AstStatement : ILocation, IAstNode
	{
		public ILocation Location { get; private set; }
		public TokenLocation Beginning => Location?.Beginning;
		public TokenLocation Ending => Location?.Ending;

		public IAstNode Parent { get; set; }

		public AstStatement(ILocation Location = null)
		{
			this.Location = Location;
		}
	}
}
