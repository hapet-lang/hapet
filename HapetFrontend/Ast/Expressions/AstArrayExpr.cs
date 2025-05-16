using HapetFrontend.Ast.Declarations;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

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
