using HapetFrontend.Ast.Statements;

namespace HapetFrontend.Ast.Expressions
{
    public class AstAddressOfExpr : AstExpression
    {
        /// <summary>
        /// The expression on which the addrof is applied
        /// </summary>
        public AstExpression SubExpression { get; set; }

        public override string AAAName => nameof(AstAddressOfExpr);

        public AstAddressOfExpr(AstExpression sub, ILocation Location)
            : base(Location)
        {
            SubExpression = sub;
        }
    }
}
