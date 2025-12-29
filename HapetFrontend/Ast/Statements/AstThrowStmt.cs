using HapetFrontend.Ast.Expressions;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Statements
{
    public class AstThrowStmt : AstStatement
    {
        /// <summary>
        /// Throw expression (the expression after 'throw' word)
        /// </summary>
        public AstExpression ThrowExpression { get; set; }

        public override string AAAName => nameof(AstThrowStmt);

        public AstThrowStmt(AstExpression expr, ILocation location = null) : base(location)
        {
            ThrowExpression = expr;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstThrowStmt(
                ThrowExpression?.GetDeepCopy() as AstExpression,
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (ThrowExpression == oldChild)
                ThrowExpression = newChild as AstExpression;
        }
    }
}
