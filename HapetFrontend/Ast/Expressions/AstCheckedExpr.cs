using System.Xml.Linq;

namespace HapetFrontend.Ast.Expressions
{
    /// <summary>
    /// The shite is like this:
    /// 'checked(int.MaxValue + 1)' - would error at comp time or at runtime when overflow
    /// 'unchecked(int.MaxValue + 1)' - won't error at any time. silent overflow
    /// '(int.MaxValue + 1)' - the same as 'unchecked'. 
    /// </summary>
    public class AstCheckedExpr : AstExpression
    {
        /// <summary>
        /// 'true' if 'checked', 'false' if 'unchecked'
        /// </summary>
        public bool IsChecked { get; set; }

        /// <summary>
        /// Sub expr of the 'checked' expr
        /// </summary>
        public AstExpression SubExpression { get; set; }

        public override string AAAName => nameof(AstCheckedExpr);

        public AstCheckedExpr(AstExpression sub, ILocation location = null) : base(location)
        {
            SubExpression = sub;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstCheckedExpr(
                SubExpression.GetDeepCopy() as AstExpression,
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                IsCompileTimeValue = IsCompileTimeValue,
                IsChecked = IsChecked,
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
            if (SubExpression == oldChild)
                SubExpression = newChild as AstExpression;
        }
    }
}
