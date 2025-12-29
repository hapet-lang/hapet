using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Statements
{
    /// <summary>
    /// Variable (or something other) assignment ast
    /// Operators like '+=' and other are prepared on parsing step so there is only '=' operator
    /// </summary>
    public class AstAssignStmt : AstStatement
    {
        public AstNestedExpr Target { get; set; }
        public AstExpression Value { get; set; }

        public override string AAAName => nameof(AstAssignStmt);

        public AstAssignStmt(AstNestedExpr target, AstExpression value, ILocation location = null)
            : base(location)
        {
            this.Target = target;
            this.Value = value;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstAssignStmt(
                Target.GetDeepCopy() as AstNestedExpr,
                Value.GetDeepCopy() as AstExpression,
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (Target == oldChild)
                Target = newChild as AstNestedExpr;
            else if (Value == oldChild)
                Value = newChild as AstExpression;
        }
    }
}
