using HapetFrontend.Ast.Expressions;
using HapetFrontend.Enums;

namespace HapetFrontend.Ast.Statements
{
    public class AstDirectiveStmt : AstStatement
    {
        /// <summary>
        /// The right part of directive
        /// </summary>
        public AstStatement RightPart { get; set; }

        /// <summary>
        /// The type of the directive
        /// </summary>
        public DirectiveType DirectiveType { get; set; }

        public override string AAAName => nameof(AstDirectiveStmt);

        public AstDirectiveStmt(AstStatement right, DirectiveType type, ILocation location = null) : base(location)
        {
            RightPart = right;
            DirectiveType = type;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstDirectiveStmt(
                RightPart.GetDeepCopy() as AstStatement,
                DirectiveType,
                Location)
            {
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
