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

        public AstArrayExpr(AstExpression sub, ILocation Location = null) : base(Location)
        {
            SubExpression = sub;
        }

        public static AstStructDecl GetArrayStruct(Scope scope)
        {
            return (scope.GetSymbolInNamespace("System", "Array") as DeclSymbol).Decl as AstStructDecl;
        }
    }
}
