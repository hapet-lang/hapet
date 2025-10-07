using HapetFrontend.Types;

namespace HapetFrontend.Ast.Expressions
{
    public class AstEmptyStructExpr : AstExpression
    {
        public override string AAAName => nameof(AstEmptyStructExpr);
        public StructType TypeForDefault { get; set; }

        public AstEmptyStructExpr(StructType tp, ILocation location = null) : base(location)
        {
            TypeForDefault = tp;
            OutType = TypeForDefault;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstEmptyStructExpr(TypeForDefault, Location)
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
