using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Statements
{
    public class AstWhileStmt : AstStatement
    {
        /// <summary>
		/// The condition param of 'while' loop. Could be pure <see cref="AstExpression"/>.
		/// Has to return <see cref="BoolType"/>
		/// </summary>
		public AstExpression ConditionParam { get; set; } 

        /// <summary>
        /// The body of for loop
        /// </summary>
        public AstBlockExpr Body { get; set; }

        public AstWhileStmt(AstExpression condition, AstBlockExpr body, ILocation location = null) : base(location)
        {
            ConditionParam = condition;
            Body = body;
        }
    }
}
