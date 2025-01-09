using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstCastExpr : AstExpression
    {
        public AstStatement SubExpression { get; set; }
        public AstStatement TypeExpr { get; set; }

        [DebuggerStepThrough]
        public AstCastExpr(AstStatement typeExpr, AstStatement sub, ILocation Location = null) : base(Location)
        {
            this.TypeExpr = typeExpr;
            SubExpression = sub;
        }
    }
}
