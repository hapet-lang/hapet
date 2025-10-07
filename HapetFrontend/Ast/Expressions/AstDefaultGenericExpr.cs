using HapetFrontend.Types;

namespace HapetFrontend.Ast.Expressions
{
    public class AstDefaultGenericExpr : AstExpression
    {
        public override string AAAName => nameof(AstDefaultGenericExpr);
        public GenericType TypeForDefault { get; set; }

        public AstDefaultGenericExpr(GenericType tp, ILocation location = null) : base(location)
        {
            TypeForDefault = tp;
            OutType = TypeForDefault;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstDefaultGenericExpr(TypeForDefault, Location)
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
