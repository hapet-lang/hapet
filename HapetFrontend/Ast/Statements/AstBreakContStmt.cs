namespace HapetFrontend.Ast.Statements
{
    public class AstBreakContStmt : AstStatement
    {
        /// <summary>
        /// 'true' if it is 'break', 'false' if it is a 'continue'
        /// </summary>
        public bool IsBreak { get; set; }

        public AstBreakContStmt(bool isBreak, ILocation Location = null) : base(Location)
        {
            IsBreak = isBreak;
        }
    }
}
