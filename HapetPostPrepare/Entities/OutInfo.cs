using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetPostPrepare.Entities
{
    internal class OutInfo
    {
        public bool ItWasProperty { get; set; }

        public bool ItWasIndexer { get; set; }
        public AstNestedExpr IndexedObject { get; set; }
        public AstExpression IndexedIndex { get; set; }

        public List<AstVarDecl> IsOpDeclarations { get; private set; } = new List<AstVarDecl>();

        public static OutInfo Default => new OutInfo()
        {
            ItWasProperty = false,
            ItWasIndexer = false,
        };
    }
}
