using HapetFrontend.Ast.Declarations;
using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstNullExpr : AstExpression
    {
        public override string AAAName => nameof(AstNullExpr);

        public AstNullExpr(HapetType target, ILocation location = null) : base(location)
        {
            OutType = new NullType(target);
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstNullExpr(
                (OutType as NullType)?.TargetType,
                Location)
            {
                IsCompileTimeValue = IsCompileTimeValue,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
            };
            return copy;
        }
    }
}
