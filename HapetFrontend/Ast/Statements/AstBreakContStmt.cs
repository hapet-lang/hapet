using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    public class AstBreakContStmt : AstStatement
    {
        /// <summary>
        /// 'true' if it is 'break', 'false' if it is a 'continue'
        /// </summary>
        public bool IsBreak { get; set; }

        public override string AAAName => nameof(AstBreakContStmt);

        public AstBreakContStmt(bool isBreak, ILocation location = null) : base(location)
        {
            IsBreak = isBreak;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstBreakContStmt(IsBreak, Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
