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
                        if (data.IntValue >= sbyte.MinValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(1, true);
                        else if (data.IntValue >= short.MinValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(2, true);
                        else if (data.IntValue >= int.MinValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(4, true);
                        else if (data.IntValue >= long.MinValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(8, true);
                        else
                            Debug.Assert(false, "Too big int");
                    }
                    else
                    {
                        if (data.IntValue <= sbyte.MaxValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(1, true);
                        else if (data.IntValue <= byte.MaxValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(1, false);
                        else if (data.IntValue <= short.MaxValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(2, true);
                        else if (data.IntValue <= ushort.MaxValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(2, false);
                        else if (data.IntValue <= int.MaxValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(4, true);
                        else if (data.IntValue <= uint.MaxValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(4, false);
                        else if (data.IntValue <= long.MaxValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(8, true);
                        else if (data.IntValue <= ulong.MaxValue)
                            OutType = HapetType.CurrentTypeContext.GetIntType(8, false);
                        else
                            Debug.Assert(false, "Too big int");
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
