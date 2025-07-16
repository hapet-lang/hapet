using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;

namespace HapetLastPrepare.Entities
{
    public class OutInfo
    {
        public bool ItWasProperty { get; set; }
        public AstPropertyDecl Property { get; set; }
        public bool IsPropertySet { get; set; }
        public bool ItWasIndexer { get; set; }
        public AstNestedExpr IndexedObject { get; set; }
        public AstExpression IndexedIndex { get; set; }

        public static OutInfo Default => new OutInfo()
        {
        };
    }
}
