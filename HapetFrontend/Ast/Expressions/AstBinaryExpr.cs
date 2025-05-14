using HapetFrontend.Ast.Declarations;
using HapetFrontend.Scoping;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstBinaryExpr : AstExpression
    {
        public string Operator { get; set; }
        public AstExpression Left { get; set; }
        public AstExpression Right { get; set; }

        /// <summary>
        /// Used probably only for 'is' bin op
        /// when 'is not' is presented
        /// </summary>
        public bool IsNot { get; set; }

        public IBinaryOperator ActualOperator { get; set; }

        public override string AAAName => nameof(AstBinaryExpr);

        public AstBinaryExpr(string op, AstExpression lhs, AstExpression rhs, ILocation location = null) : base(location)
        {
            Operator = op;
            Left = lhs;
            Right = rhs;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstBinaryExpr(
                Operator,
                Left.GetDeepCopy() as AstExpression,
                Right.GetDeepCopy() as AstExpression,
                Location)
            {
                ActualOperator = ActualOperator,
                IsCompileTimeValue = IsCompileTimeValue,
                IsNot = IsNot,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
