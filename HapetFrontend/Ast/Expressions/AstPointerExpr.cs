using HapetFrontend.Ast.Declarations;

namespace HapetFrontend.Ast.Expressions
{
    // also used for dereference
    public class AstPointerExpr : AstExpression
    {
        /// <summary>
        /// The expression on which the pointer is applied
        /// </summary>
        public AstExpression SubExpression { get; set; }

        /// <summary>
        /// The '*' could also be used for dereferece variables and other shite. 
        /// So you should check how was the pointer applied - to a type or to a name of smth?
        /// Was it on the right side or the left side?
        /// </summary>
        public bool IsDereference { get; set; } = false;

        public override string AAAName => nameof(AstPointerExpr);

        public AstPointerExpr(AstExpression sub, bool isDeref = false, ILocation location = null)
            : base(location)
        {
            IsDereference = isDeref;
            SubExpression = sub;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstPointerExpr(
                SubExpression.GetDeepCopy() as AstExpression,
                IsDereference,
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
