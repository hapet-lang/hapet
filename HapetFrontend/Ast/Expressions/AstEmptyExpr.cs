namespace HapetFrontend.Ast.Expressions
{
    public class AstEmptyExpr : AstExpression
    {
        public override string AAAName => nameof(AstEmptyExpr);

        public AstEmptyExpr(ILocation location = null) : base(location)
        {
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstEmptyExpr(Location)
            {
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
