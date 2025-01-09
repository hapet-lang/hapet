using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    public class AstBaseCtorStmt : AstStatement
    {
        /// <summary>
		/// The arguments to be passed into base ctor
		/// </summary>
		public List<AstArgumentExpr> Arguments { get; set; }

        public AstBaseCtorStmt(List<AstArgumentExpr> arguments = null, ILocation Location = null)
            : base(Location)
        {
            this.Arguments = arguments ?? new List<AstArgumentExpr>();
        }
    }
}
