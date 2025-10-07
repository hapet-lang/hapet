using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Statements
{
    public class AstDoWhileStmt : AstStatement
    {
        /// <summary>
		/// The condition param of 'while' loop. Could be pure <see cref="AstExpression"/>.
		/// Has to return <see cref="BoolType"/>
		/// </summary>
		public AstExpression Condition { get; set; }

        /// <summary>
        /// The body of for loop
        /// </summary>
        public AstBlockExpr Body { get; set; }

        public override string AAAName => nameof(AstDoWhileStmt);

        public AstDoWhileStmt(AstExpression condition, AstBlockExpr body, ILocation location = null) : base(location)
        {
            Condition = condition;
            Body = body;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstDoWhileStmt(
                Condition.GetDeepCopy() as AstExpression,
                Body.GetDeepCopy() as AstBlockExpr,
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
