using HapetFrontend.Ast.Declarations;
using HapetFrontend.Scoping;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstUnaryExpr : AstExpression
    {
        public string Operator { get; set; }
        public AstExpression SubExpr { get; set; }

        public IUnaryOperator ActualOperator { get; set; } = null;

        public override string AAAName => nameof(AstUnaryExpr);

        public AstUnaryExpr(string op, AstExpression sub, ILocation location = null) : base(location)
        {
            Operator = op;
            SubExpr = sub;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstUnaryExpr(
                Operator,
                SubExpr.GetDeepCopy() as AstExpression,
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
