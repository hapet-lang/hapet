using HapetFrontend.Ast.Declarations;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstCastExpr : AstExpression
    {
        public AstExpression SubExpression { get; set; }
        public AstExpression TypeExpr { get; set; }

        public override string AAAName => nameof(AstCastExpr);

        public AstCastExpr(AstExpression typeExpr, AstExpression sub, ILocation location = null) : base(location)
        {
            this.TypeExpr = typeExpr;
            SubExpression = sub;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstCastExpr(
                TypeExpr.GetDeepCopy() as AstExpression,
                SubExpression.GetDeepCopy() as AstExpression,
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
            };
            return copy;
        }
    }
}
