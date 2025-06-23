using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetPostPrepare.Entities
{
    public class OutInfo
    {
        public AstNestedExpr IndexedObject { get; set; }
        public AstExpression IndexedIndex { get; set; }

        public static OutInfo Default => new OutInfo()
        {
        };
    }
}
