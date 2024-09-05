using HapetFrontend.Entities;
using HapetFrontend.Scoping;

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

		/// <summary>
		/// In which scope it could be accessable
		/// </summary>
		public Scope Scope { get; set; }

		/// <summary>
		/// The file in which the statement is located
		/// </summary>
		public ProgramFile SourceFile { get; set; }

		public IAstNode Parent { get; set; }

		public AstStatement(ILocation Location = null)
		{
			this.Location = Location;
		}
	}
}
