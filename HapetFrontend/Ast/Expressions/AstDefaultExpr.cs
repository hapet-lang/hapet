using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstDefaultExpr : AstExpression
    {
        public override string AAAName => nameof(AstDefaultExpr);
        public AstExpression TypeForDefault { get; set; }

        public AstDefaultExpr(ILocation location = null) : base(location)
        {
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstDefaultExpr(Location)
            {
                IsCompileTimeValue = IsCompileTimeValue,
                TypeForDefault = TypeForDefault,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }

        public static AstExpression GetDefaultValueForType(HapetType tp, AstExpression orig, IMessageHandler messageHandler)
        {
            AstExpression outExpr;
            switch (tp)
            {
                case IntType:
                    outExpr = new AstNumberExpr(NumberData.FromInt(0), null, tp, orig);
                    break;
                case FloatType:
                    outExpr = new AstNumberExpr(NumberData.FromDouble(0), null, tp, orig);
                    break;
                case CharType:
                    outExpr = new AstCharExpr("", orig);
                    break;
                case BoolType:
                    outExpr = new AstBoolExpr(false, orig);
                    break;
                case ClassType:
                case PointerType:
                case ArrayType:
                case StringType:
                    outExpr = new AstNullExpr(tp, orig);
                    break;
                case StructType st:
                    outExpr = new AstEmptyStructExpr(st, orig);
                    break;
                case GenericType gt:
                    outExpr = new AstDefaultGenericExpr(gt, orig);
                    break;
                default:
                    messageHandler.ReportMessage(orig.SourceFile.Text, orig, [HapetType.AsString(tp)], ErrorCode.Get(CTEN.NoDefaultValueForType));
                    outExpr = null;
                    break;
            }
            if (outExpr != null && orig != null)
                outExpr.Scope = orig.Scope;
            return outExpr;
        }
    }
}
