using HapetFrontend.Scoping;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstBinaryExpr : AstExpression
    {
        public string Operator { get; set; }
        public AstStatement Left { get; set; }
        public AstStatement Right { get; set; }

        public IBinaryOperator ActualOperator { get; set; }

        [DebuggerStepThrough]
        public AstBinaryExpr(string op, AstStatement lhs, AstStatement rhs, ILocation Location = null) : base(Location)
        {
            Operator = op;
            Left = lhs;
            Right = rhs;
        }
    }
}
