namespace HapetFrontend.Ast.Expressions
{
    public class AstNullableExpr : AstExpression
    {
        /// <summary>
        /// The expression on which the nullable is applied
        /// </summary>
        public AstExpression SubExpression { get; set; }

        public override string AAAName => nameof(AstNullableExpr);

        public AstNullableExpr(AstExpression sub, ILocation location = null)
            : base(location)
        {
            SubExpression = sub;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstNullableExpr(
                SubExpression.GetDeepCopy() as AstExpression,
                Location)
            {
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
            };
            return copy;
        }
    }
}
