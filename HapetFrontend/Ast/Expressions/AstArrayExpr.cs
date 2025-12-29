using HapetFrontend.Ast.Declarations;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Expressions
{
    public class AstArrayExpr : AstExpression
    {
        /// <summary>
        /// The expression on which the array is applied
        /// </summary>
        public AstExpression SubExpression { get; set; }

        public override string AAAName => nameof(AstArrayExpr);

        public AstArrayExpr(AstExpression sub, ILocation location = null) : base(location)
        {
            SubExpression = sub;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstArrayExpr(
                SubExpression.GetDeepCopy() as AstExpression,
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
            if (SubExpression == oldChild)
                SubExpression = newChild as AstExpression;
        }
    }
}
