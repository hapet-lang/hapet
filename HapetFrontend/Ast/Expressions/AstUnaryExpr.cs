using HapetFrontend.Scoping;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstUnaryExpr : AstExpression
    {
        public string Operator { get; set; }
        public AstStatement SubExpr { get; set; }

        public IUnaryOperator ActualOperator { get; set; } = null;

        [DebuggerStepThrough]
        public AstUnaryExpr(string op, AstStatement sub, ILocation Location = null) : base(Location)
        {
            Operator = op;
            SubExpr = sub;
        }
    }
}
