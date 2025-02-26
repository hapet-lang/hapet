using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Statements
{
    public class AstIfStmt : AstStatement
    {
        /// <summary>
        /// The condition of 'if' stmt. Could be pure <see cref="AstExpression"/>.
        /// Has to return <see cref="BoolType"/>
        /// </summary>
        public AstExpression Condition { get; set; }

        /// <summary>
        /// The body executed when condition is true
        /// </summary>
        public AstBlockExpr BodyTrue { get; set; }

        /// <summary>
        /// The body executed when condition is false
        /// </summary>
        public AstBlockExpr BodyFalse { get; set; }

        public override string AAAName => nameof(AstIfStmt);

        public AstIfStmt(AstExpression cond, AstBlockExpr bodyTrue, AstBlockExpr bodyFalse, ILocation location = null) : base(location)
        {
            Condition = cond;
            BodyTrue = bodyTrue;
            BodyFalse = bodyFalse;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstIfStmt(
                Condition.GetDeepCopy() as AstExpression,
                BodyTrue.GetDeepCopy() as AstBlockExpr,
                BodyFalse?.GetDeepCopy() as AstBlockExpr,
                Location)
            {
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
