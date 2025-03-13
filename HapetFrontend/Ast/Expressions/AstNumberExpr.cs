using HapetFrontend.Ast.Declarations;
using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstNumberExpr : AstExpression
    {
        public NumberData Data { get; private set; }
        public string Suffix { get; set; }

        public override string AAAName => nameof(AstNumberExpr);

        public AstNumberExpr(NumberData data, string suffix = null, HapetType numberType = null, ILocation location = null) : base(location)
        {
            Data = data;
            OutValue = data;
            this.Suffix = suffix;

            if (numberType == null)
            {
                if (data.Type == Enums.NumberType.Float)
                {
                    OutType = FloatType.DefaultType;
                }
                else
                {
                    OutType = IntType.DefaultType;
                }
            }
            else
                OutType = numberType;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstNumberExpr(
                Data, Suffix, OutType,
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
