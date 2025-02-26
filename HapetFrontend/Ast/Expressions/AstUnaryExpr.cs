using HapetFrontend.Ast.Declarations;
using HapetFrontend.Scoping;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstUnaryExpr : AstExpression
    {
        public string Operator { get; set; }
        public AstStatement SubExpr { get; set; }

        public IUnaryOperator ActualOperator { get; set; } = null;

        public override string AAAName => nameof(AstUnaryExpr);

        public AstUnaryExpr(string op, AstStatement sub, ILocation Location = null) : base(Location)
        {
            Operator = op;
            SubExpr = sub;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstUnaryExpr(
                Operator,
                SubExpr.GetDeepCopy() as AstStatement,
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
