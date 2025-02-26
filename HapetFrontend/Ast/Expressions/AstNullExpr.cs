using HapetFrontend.Ast.Declarations;
using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstNullExpr : AstExpression
    {
        /// <summary>
        /// The target type of null
        /// </summary>
        public HapetType Target { get; set; }

        public override string AAAName => nameof(AstNullExpr);

        public AstNullExpr(HapetType target, ILocation Location = null) : base(Location)
        {
            Target = target;
            OutType = PointerType.NullLiteralType;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstNullExpr(
                Target,
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
