using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    public class AstEmptyStmt : AstStatement
    {
        public override string AAAName => nameof(AstEmptyStmt);

        public AstEmptyStmt(ILocation Location = null) : base(Location: Location) { }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstEmptyStmt(Location)
            {
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
