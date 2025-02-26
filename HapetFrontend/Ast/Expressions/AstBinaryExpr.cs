using HapetFrontend.Ast.Declarations;
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

        public override string AAAName => nameof(AstBinaryExpr);

        public AstBinaryExpr(string op, AstStatement lhs, AstStatement rhs, ILocation Location = null) : base(Location)
        {
            Operator = op;
            Left = lhs;
            Right = rhs;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstBinaryExpr(
                Operator,
                Left.GetDeepCopy() as AstStatement,
                Right.GetDeepCopy() as AstStatement,
                Location)
            {
                ActualOperator = ActualOperator,
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
