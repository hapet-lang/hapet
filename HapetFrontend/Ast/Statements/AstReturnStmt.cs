using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    public class AstReturnStmt : AstStatement
    {
        /// <summary>
        /// Return expression (the expression after 'return' word)
        /// </summary>
        public AstExpression ReturnExpression { get; set; }

        /// <summary>
        /// 'true' for => in lambdas because it could be and could not be return - inference required
        /// </summary>
        public bool IsWeakReturn { get; set; }

        /// <summary>
        /// Statement of weak return
        /// </summary>
        public AstStatement WeakReturnStatement { get; set; }

        /// <summary>
        /// Used in LSP that 'return' word should not be colorized
        /// </summary>
        public bool IsArrowedReturn { get; set; }

        public override string AAAName => nameof(AstReturnStmt);

        public AstReturnStmt(AstExpression expr, ILocation location = null) : base(location)
        {
            ReturnExpression = expr;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstReturnStmt(
                ReturnExpression?.GetDeepCopy() as AstExpression,
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                IsWeakReturn = IsWeakReturn,
                WeakReturnStatement = WeakReturnStatement?.GetDeepCopy() as AstStatement,
                Scope = Scope,
                SourceFile = SourceFile,
                IsArrowedReturn = IsArrowedReturn,
            };
            return copy;
        }
    }
}
