using HapetFrontend.Ast.Declarations;

namespace HapetFrontend.Ast.Expressions
{
    public class AstArgumentExpr : AstExpression
    {
        /// <summary>
        /// The expression itself
        /// </summary>
        public AstExpression Expr { get; set; }
        /// <summary>
        /// The name of a parameter into which argument is passed
        /// </summary>
        public AstIdExpr Name { get; set; }

        public override string AAAName => nameof(AstArgumentExpr);

        public AstArgumentExpr(AstExpression expr, AstIdExpr name = null, ILocation location = null)
            : base(location)
        {
            this.Expr = expr;
            this.Name = name;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstArgumentExpr(
                Expr.GetDeepCopy() as AstExpression,
                Name?.GetDeepCopy() as AstIdExpr,
                Location)
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
