using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Statements
{
    public class AstBaseCtorStmt : AstStatement
    {
        /// <summary>
        /// The arguments to be passed into base ctor
        /// </summary>
        public List<AstArgumentExpr> Arguments { get; set; }

        /// <summary>
        /// The type of base class
        /// </summary>
        public ClassType BaseType { get; set; }

        /// <summary>
        /// Instance of current class. Needed for generation part
        /// </summary>
        public AstIdExpr ThisArgument { get; set; }

        /// <summary>
        /// 'true' if it is a 'this' ctor call
        /// </summary>
        public bool IsThisCtorCall { get; set; }

        public override string AAAName => nameof(AstBaseCtorStmt);

        public AstBaseCtorStmt(List<AstArgumentExpr> arguments = null, ILocation location = null)
            : base(location)
        {
            this.Arguments = arguments ?? new List<AstArgumentExpr>();
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstBaseCtorStmt(
                Arguments.Select(x => x.GetDeepCopy() as AstArgumentExpr).ToList(),
                Location)
            {
                BaseType = BaseType,
                IsThisCtorCall = IsThisCtorCall,
                ThisArgument = ThisArgument?.GetDeepCopy() as AstIdExpr,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
