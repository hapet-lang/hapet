using HapetFrontend.Ast.Declarations;
using HapetFrontend.Scoping;

namespace HapetFrontend.Ast.Expressions
{
    public class AstBlockExpr : AstExpression
    {
        /// <summary>
        /// The statements that are in the block
        /// </summary>
        public List<AstStatement> Statements { get; set; }

        /// <summary>
        /// The inner scope of the block. Used to get access to it's content
        /// </summary>
        public Scope SubScope { get; set; }

        public override string AAAName => nameof(AstBlockExpr);

        public AstBlockExpr(List<AstStatement> statements, ILocation location = null) : base(location)
        {
            Statements = statements;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstBlockExpr(
                Statements.Select(x => x.GetDeepCopy() as AstStatement).ToList(),
                Location)
            {
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
            };
            return copy;
        }
    }
}
