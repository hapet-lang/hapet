using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstArrayAccessExpr : AstExpression
    {
        /// <summary>
        /// The expression on which indexing is done
        /// </summary>
        public AstExpression ObjectName { get; set; }
        /// <summary>
        /// It could be not only an Int. but also a String (for dicts) and other shite
        /// For ndim arrays use nested of this
        /// </summary>
        public AstExpression ParameterExpr { get; set; }

        [DebuggerStepThrough]
        public AstArrayAccessExpr(AstExpression objectName, AstExpression parameterExpr, ILocation Location = null) : base(Location)
        {
            ObjectName = objectName;
            ParameterExpr = parameterExpr;
        }
    }
}
