using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    public class AstAttributeStmt : AstStatement
    {
        /// <summary>
        /// The name of the attribute
        /// </summary>
        public AstNestedExpr AttributeName { get; set; }

        /// <summary>
        /// Arguments of the attribute
        /// </summary>
        public List<AstArgumentExpr> Arguments { get; set; }

        public override string AAAName => nameof(AstAttributeStmt);

        public AstAttributeStmt(AstNestedExpr attrName, List<AstArgumentExpr> args, ILocation location = null) : base(location)
        {
            AttributeName = attrName;
            Arguments = args;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstAttributeStmt(
                AttributeName.GetDeepCopy() as AstNestedExpr,
                Arguments.Select(x => x.GetDeepCopy() as AstArgumentExpr).ToList(),
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
