using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    public class AstThrowStmt : AstStatement
    {
        /// <summary>
        /// Throw expression (the expression after 'throw' word)
        /// </summary>
        public AstNewExpr ThrowExpression { get; set; }

        public override string AAAName => nameof(AstThrowStmt);

        public AstThrowStmt(AstNewExpr expr, ILocation location = null) : base(location)
        {
            ThrowExpression = expr;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstThrowStmt(
                ThrowExpression?.GetDeepCopy() as AstNewExpr,
                Location)
            {
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
