namespace HapetFrontend.Ast.Expressions
{
    /// <summary>
    /// Handle for sizeof, alignof, typeof intrinsics
    /// </summary>
    public class AstSATOfExpr : AstExpression
    {
        public enum SATType
        {
            Sizeof,
            Alignof,
            Typeof,
        }

        public SATType ExprType { get; set; }

        public AstSATOfExpr(SATType exprType, ILocation location = null) : base(location)
        {
            ExprType = exprType;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstSATOfExpr(ExprType, Location)
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
