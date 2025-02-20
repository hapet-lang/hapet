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
    }
}
