namespace HapetFrontend.Ast.Statements
{
    public class AstEmptyStmt : AstStatement
    {
        public override string AAAName => nameof(AstEmptyStmt);

        public AstEmptyStmt(ILocation Location = null) : base(Location: Location) { }
    }
}
