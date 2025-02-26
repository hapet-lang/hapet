using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    public class AstReturnStmt : AstStatement
    {
        /// <summary>
        /// Return expression (the expression after 'return' word)
        /// </summary>
        public AstExpression ReturnExpression { get; set; }

        public override string AAAName => nameof(AstReturnStmt);

        public AstReturnStmt(AstExpression expr, ILocation Location = null) : base(Location)
        {
            ReturnExpression = expr;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstReturnStmt(
                ReturnExpression?.GetDeepCopy() as AstExpression,
                Location)
            {
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
