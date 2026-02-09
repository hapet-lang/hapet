using HapetFrontend.Ast;
using HapetFrontend.Errors;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using System.Numerics;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void CheckComptimeCastInCheckedContext(object value, HapetType type, ILocation castLocation)
        {
            // these checks are only for int overflow
            if (type is not IntType && type is not CharType)
                return;

            var minMax = GetMinMaxOfType(type);
            bool isInRange = CompareMinMaxNanInf(value, minMax);
            if (!isInRange)
            {
                // error overflow
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, castLocation, 
                    [value.ToString(), HapetType.AsString(type)], ErrorCode.Get(CTEN.CheckedCastOverflow));
            }
        }

        private void CheckComptimeBinaryInCheckedContext(IBinaryOperator binOp, object left, object right, ILocation castLocation)
        {
            // skip if not built in and not +/-/*
            if (binOp is not BuiltInBinaryOperator builtIn || (builtIn.Name != "+" && builtIn.Name != "-" && builtIn.Name != "*"))
                return;
            // skip not the same types
            if (builtIn.LhsType != builtIn.RhsType)
                return;

            var (isOverflow, resValue) = IsBinOverflow(builtIn, left, right);
            if (isOverflow)
            {
                // error overflow
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, castLocation,
                    [resValue.ToString(), HapetType.AsString(builtIn.ResultType)], ErrorCode.Get(CTEN.CheckedCastOverflow));
            }
        }

        private (bool, BigInteger) IsBinOverflow(BuiltInBinaryOperator builtIn, object left, object right)
        {
            switch (builtIn.Name)
            {
                case "+":
                    var res = NumberData.FromObject(left) + NumberData.FromObject(right);
                    return (!CompareMinMaxNanInf(res.IntValue, GetMinMaxOfType(builtIn.ResultType)), res.IntValue);
                case "-":
                    var res2 = NumberData.FromObject(left) - NumberData.FromObject(right);
                    return (!CompareMinMaxNanInf(res2.IntValue, GetMinMaxOfType(builtIn.ResultType)), res2.IntValue);
                case "*":
                    var res3 = NumberData.FromObject(left) * NumberData.FromObject(right);
                    return (!CompareMinMaxNanInf(res3.IntValue, GetMinMaxOfType(builtIn.ResultType)), res3.IntValue);
            }
            throw new NotImplementedException();
        }

        private bool CompareMinMaxNanInf(object value, (BigInteger, BigInteger) minMax)
        {
            switch (value)
            {
                case char v: return v >= minMax.Item1 && v <= minMax.Item2;
                case byte v: return v >= minMax.Item1 && v <= minMax.Item2;
                case sbyte v: return v >= minMax.Item1 && v <= minMax.Item2;
                case ushort v: return v >= minMax.Item1 && v <= minMax.Item2;
                case short v: return v >= minMax.Item1 && v <= minMax.Item2;
                case uint v: return v >= minMax.Item1 && v <= minMax.Item2;
                case int v: return v >= minMax.Item1 && v <= minMax.Item2;
                case ulong v: return v >= minMax.Item1 && v <= minMax.Item2;
                case long v: return v >= minMax.Item1 && v <= minMax.Item2;
                case float v: 
                    bool notCringe = !float.IsNaN(v) && v != float.NegativeInfinity && v != float.PositiveInfinity;
                    return v >= (float)minMax.Item1 && v <= (float)minMax.Item2 && notCringe;
                case double v:
                    bool notCringe2 = !double.IsNaN(v) && v != double.NegativeInfinity && v != double.PositiveInfinity;
                    return v >= (double)minMax.Item1 && v <= (double)minMax.Item2 && notCringe2;
                case BigInteger v: return v >= minMax.Item1 && v <= minMax.Item2;
            }
            throw new NotImplementedException();
        }

        private (BigInteger, BigInteger) GetMinMaxOfType(HapetType type)
        {
            switch (type)
            {
                case CharType:
                    return (0, ushort.MaxValue);
                case IntType it:
                    return it.GetSize() switch
                    {
                        1 => (it.Signed ? sbyte.MinValue : byte.MinValue, it.Signed ? sbyte.MaxValue : byte.MaxValue),
                        2 => (it.Signed ? short.MinValue : ushort.MinValue, it.Signed ? short.MaxValue : ushort.MaxValue),
                        4 => (it.Signed ? int.MinValue : uint.MinValue, it.Signed ? int.MaxValue : uint.MaxValue),
                        8 => (it.Signed ? long.MinValue : ulong.MinValue, it.Signed ? long.MaxValue : ulong.MaxValue),
                        _ => throw new NotImplementedException()
                    };
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
