using HapetFrontend.Ast.Expressions;
using HapetFrontend.Enums;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Statements
{
    public class AstDirectiveStmt : AstStatement
    {
        /// <summary>
        /// The right part of directive
        /// </summary>
        public AstIdExpr RightPart { get; set; }

        /// <summary>
        /// Value of directive define
        /// </summary>
        public AstExpression Value { get; set; }

        /// <summary>
        /// The type of the directive
        /// </summary>
        public DirectiveType DirectiveType { get; set; }

        public override string AAAName => nameof(AstDirectiveStmt);

        public AstDirectiveStmt(AstIdExpr right, DirectiveType type, ILocation location = null) : base(location)
        {
            RightPart = right;
            DirectiveType = type;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstDirectiveStmt(
                RightPart.GetDeepCopy() as AstIdExpr,
                DirectiveType,
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                Scope = Scope,
                Value = Value.GetDeepCopy() as AstExpression,
                SourceFile = SourceFile,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (RightPart == oldChild)
                RightPart = newChild as AstIdExpr;
            else if (Value == oldChild)
                Value = newChild as AstExpression;
        }
    }
}
