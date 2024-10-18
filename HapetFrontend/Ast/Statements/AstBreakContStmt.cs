namespace HapetFrontend.Ast.Statements
{
	public class AstBreakContStmt : AstStatement
	{
		/// <summary>
		/// 'true' if it is 'break', 'false' if it is a 'continue'
		/// </summary>
		public bool IsBreak { get; set; }

		/// <summary>
		/// Is the stmt if used for switch-case shite
		/// </summary>
		public bool IsSwitchParent { get; set; }

		public AstBreakContStmt(bool isBreak, ILocation Location = null) : base(Location)
		{
			IsBreak = isBreak;
		}
	}
}
