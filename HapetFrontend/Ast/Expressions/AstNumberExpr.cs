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
                    OutType = HapetType.CurrentTypeContext.GetFloatType(8); // default is double
                }
                else
                {
                    // check if signed
                    if (data.IntValue < 0)
                    {
                        if (data.IntValue >= int.MinValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(4, true);
                        else
                            OutType = HapetType.CurrentTypeContext.GetIntType(8, true);
                    }
                    else
                    {
                        if (data.IntValue <= int.MaxValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(4, true);
                        else
                            OutType = HapetType.CurrentTypeContext.GetIntType(8, true);
                    }
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
