using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    public class AstEmptyStmt : AstStatement
    {
        public override string AAAName => nameof(AstEmptyStmt);

        public AstEmptyStmt(ILocation location = null) : base(location) { }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstEmptyStmt(Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            throw new NotImplementedException();
        }
    }
}
