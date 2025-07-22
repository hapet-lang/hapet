using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetPostPrepare.Entities
{
    public class OutInfo
    {
        /// <summary>
        /// Just to handle weak return inference - check usage
        /// </summary>
        public Stack<AstStatement> NeedToAddFromWeakReturn { get; } = new Stack<AstStatement>();

        public static OutInfo Default => new OutInfo()
        {
        };
    }
}
