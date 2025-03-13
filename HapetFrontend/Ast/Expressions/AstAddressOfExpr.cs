using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;

namespace HapetFrontend.Ast.Expressions
{
    public class AstAddressOfExpr : AstExpression
    {
        /// <summary>
        /// The expression on which the addrof is applied
        /// </summary>
        public AstExpression SubExpression { get; set; }

        public override string AAAName => nameof(AstAddressOfExpr);

        public AstAddressOfExpr(AstExpression sub, ILocation location)
            : base(location)
        {
            SubExpression = sub;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstAddressOfExpr(
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
