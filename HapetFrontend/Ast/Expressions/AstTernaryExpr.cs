using HapetFrontend.Types;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Expressions
{
    public class AstTernaryExpr : AstExpression
    {
        /// <summary>
        /// The condition of ternary. Could be pure <see cref="AstExpression"/>.
        /// Has to return <see cref="BoolType"/>
        /// </summary>
        public AstExpression Condition { get; set; }

        /// <summary>
        /// The expr executed when condition is true
        /// </summary>
        public AstExpression TrueExpr { get; set; }

        /// <summary>
        /// The expr executed when condition is false
        /// </summary>
        public AstExpression FalseExpr { get; set; }

        public override string AAAName => nameof(AstTernaryExpr);

        public AstTernaryExpr(AstExpression cond, AstExpression tr, AstExpression fl, ILocation location = null) : base(location)
        {
            Condition = cond;
            TrueExpr = tr;
            FalseExpr = fl;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstTernaryExpr(
                Condition.GetDeepCopy() as AstExpression, 
                TrueExpr.GetDeepCopy() as AstExpression, 
                FalseExpr.GetDeepCopy() as AstExpression, 
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

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (Condition == oldChild)
                Condition = newChild as AstExpression;
            else if (TrueExpr == oldChild)
                TrueExpr = newChild as AstExpression;
            else if (FalseExpr == oldChild)
                FalseExpr = newChild as AstExpression;
        }
    }
}
