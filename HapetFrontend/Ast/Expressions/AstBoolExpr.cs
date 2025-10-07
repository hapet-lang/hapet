using HapetFrontend.Ast.Declarations;
using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstBoolExpr : AstExpression
    {
        public bool BoolValue => (bool)OutValue;

        public override string AAAName => nameof(AstBoolExpr);

        public AstBoolExpr(bool value, ILocation location = null) : base(location)
        {
            this.OutType = HapetType.CurrentTypeContext.BoolTypeInstance;
            this.OutValue = value;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstBoolExpr(
                BoolValue,
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
