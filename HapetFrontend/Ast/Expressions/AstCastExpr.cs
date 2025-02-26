using HapetFrontend.Ast.Declarations;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstCastExpr : AstExpression
    {
        public AstStatement SubExpression { get; set; }
        public AstStatement TypeExpr { get; set; }

        public override string AAAName => nameof(AstCastExpr);

        public AstCastExpr(AstStatement typeExpr, AstStatement sub, ILocation Location = null) : base(Location)
        {
            this.TypeExpr = typeExpr;
            SubExpression = sub;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstCastExpr(
                TypeExpr.GetDeepCopy() as AstStatement,
                SubExpression.GetDeepCopy() as AstStatement,
                Location)
            {
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
