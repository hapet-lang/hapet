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
		public ILocation Location { get; set; }
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

		/// <summary>
		/// Parent ast node
		/// </summary>
		public IAstNode Parent { get; set; }
		/// <summary>
		/// Parent ast node as AstStatement
		/// </summary>
		public AstStatement NormalParent => Parent as AstStatement;

		public AstStatement(ILocation Location = null)
		{
			this.Location = Location;
		}
	}
}
