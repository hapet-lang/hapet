using HapetFrontend.Types;

namespace HapetFrontend.Ast.Expressions
{
    public class AstTernaryExpr : AstExpression
    {
        /// <summary>
        /// The condition of ternary. Could be pure <see cref="AstExpression"/>.
        /// Has to return <see cref="BoolType"/>
        /// </summary>
        public AstExpression Condition { get; set; }

        /// <summary>
        /// The expr executed when condition is true
        /// </summary>
        public AstExpression TrueExpr { get; set; }

        /// <summary>
        /// The expr executed when condition is false
        /// </summary>
        public AstExpression FalseExpr { get; set; }

        public override string AAAName => nameof(AstTernaryExpr);

        public AstTernaryExpr(AstExpression cond, AstExpression tr, AstExpression fl, ILocation location = null) : base(location)
        {
            Condition = cond;
            TrueExpr = tr;
            FalseExpr = fl;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstTernaryExpr(Condition, TrueExpr, FalseExpr, Location)
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
