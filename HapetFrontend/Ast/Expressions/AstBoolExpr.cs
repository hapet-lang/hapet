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
            this.OutType = BoolType.Instance;
            this.OutValue = value;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstBoolExpr(
                BoolValue,
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
