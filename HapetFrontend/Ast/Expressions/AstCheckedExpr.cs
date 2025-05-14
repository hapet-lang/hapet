namespace HapetFrontend.Ast.Expressions
{
    public class AstCheckedExpr : AstExpression
    {
        /// <summary>
        /// 'true' if 'checked', 'false' if 'unchecked'
        /// </summary>
        public bool IsChecked { get; set; }

        /// <summary>
        /// Sub expr of the 'checked' expr
        /// </summary>
        public AstExpression SubExpression { get; set; }

        public AstCheckedExpr(AstExpression sub, ILocation location = null) : base(location)
        {
            SubExpression = sub;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstCheckedExpr(
                SubExpression.GetDeepCopy() as AstExpression,
                Location)
            {
                IsCompileTimeValue = IsCompileTimeValue,
                IsChecked = IsChecked,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
