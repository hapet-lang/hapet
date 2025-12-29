using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using System.Diagnostics;
using System.Xml.Linq;

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
                IsSyntheticStatement = IsSyntheticStatement,
                IsCompileTimeValue = IsCompileTimeValue,
                TypeForDefault = TypeForDefault?.GetDeepCopy() as AstExpression,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (TypeForDefault == oldChild)
                TypeForDefault = newChild as AstExpression;
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
                    outExpr = new AstNullExpr(tp, orig);
                    break;
                case StructType st:
                    outExpr = new AstEmptyStructExpr(st, orig);
                    break;
                case GenericType gt:
                    outExpr = new AstDefaultGenericExpr(gt, orig);
                    break;
                case EnumType en:
                    outExpr = new AstNumberExpr(NumberData.FromInt(0), null, en.Declaration.InheritedType.OutType, orig);
                    outExpr.SetDataFromStmt(orig);
                    var tpp = new AstNestedExpr(en.Declaration.Name.GetDeepCopy() as AstIdExpr, null, orig);
                    outExpr = new AstCastExpr(tpp, outExpr, orig);
                    outExpr.SetDataFromStmt(orig, true);
                    tpp.SetDataFromStmt(orig, true);
                    outExpr.OutType = en.Declaration.Type.OutType;
                    break;
                default:
                    messageHandler.ReportMessage(orig.SourceFile, orig, [HapetType.AsString(tp)], ErrorCode.Get(CTEN.NoDefaultValueForType));
                    outExpr = null;
                    break;
            }
            if (outExpr != null && orig != null)
                outExpr.Scope = orig.Scope;
            return outExpr;
        }
    }
}
