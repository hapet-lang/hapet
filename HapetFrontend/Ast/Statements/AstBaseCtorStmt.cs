using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    public class AstBaseCtorStmt : AstStatement
    {
        /// <summary>
        /// The arguments to be passed into base ctor
        /// </summary>
        public List<AstArgumentExpr> Arguments { get; set; }

        public AstBaseCtorStmt(List<AstArgumentExpr> arguments = null, ILocation location = null)
            : base(location)
        {
            this.Arguments = arguments ?? new List<AstArgumentExpr>();
        }
    }
}
