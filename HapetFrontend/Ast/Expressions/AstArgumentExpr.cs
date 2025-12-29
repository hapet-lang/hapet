using HapetFrontend.Ast.Declarations;
using HapetFrontend.Enums;

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
        /// The parameter modificator like 'ref', 'out', etc.
        /// </summary>
        public ParameterModificator ArgumentModificator { get; set; } = ParameterModificator.None;

        /// <summary>
        /// Used in LSP
        /// </summary>
        public ILocation ArgModificatorLocation { get; set; }

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
                IsSyntheticStatement = IsSyntheticStatement,
                ArgumentModificator = ArgumentModificator,
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
                ArgModificatorLocation = ArgModificatorLocation,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (Expr == oldChild)
                Expr = newChild as AstExpression;
            else if (Name == oldChild)
                Name = newChild as AstIdExpr;
        }
    }
}
