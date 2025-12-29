using HapetFrontend.Ast.Expressions;
using HapetFrontend.Enums;
using System.Diagnostics;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Statements
{
    public class AstConstrainStmt : AstStatement
    {
        /// <summary>
        /// The type of the constrain
        /// </summary>
        public GenericConstrainType ConstrainType { get; set; }

        /// <summary>
        /// The expr of the constrain like 'MyNs.IAnime'
        /// </summary>
        public AstNestedExpr Expr { get; set; }

        /// <summary>
        /// At least used for 'new(int, string)' shite
        /// </summary>
        public List<AstNestedExpr> AdditionalExprs { get; set; }

        public override string AAAName => nameof(AstConstrainStmt);

        public AstConstrainStmt(AstNestedExpr expr, GenericConstrainType type, ILocation location = null) : base(location)
        {
            Expr = expr;
            ConstrainType = type;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstConstrainStmt(
                Expr?.GetDeepCopy() as AstNestedExpr,
                ConstrainType,
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                AdditionalExprs = AdditionalExprs.Select(x => x.GetDeepCopy() as AstNestedExpr).ToList(),
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (Expr == oldChild)
                Expr = newChild as AstNestedExpr;
            else if (AdditionalExprs.Contains(oldChild))
            {
                int index = AdditionalExprs.IndexOf(oldChild as AstNestedExpr);
                AdditionalExprs[index] = newChild as AstNestedExpr;
            }
        }
    }
}
