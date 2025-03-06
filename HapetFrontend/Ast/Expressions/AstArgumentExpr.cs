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
        /// <summary>
        /// Index of the argument
        /// </summary>
        public int Index { get; set; } = -1;

        public override string AAAName => nameof(AstArgumentExpr);

        public AstArgumentExpr(AstExpression expr, AstIdExpr name = null, ILocation Location = null)
            : base(Location)
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
                Index = Index,
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
