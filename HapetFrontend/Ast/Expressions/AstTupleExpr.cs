using System.Xml.Linq;

namespace HapetFrontend.Ast.Expressions
{
    public class AstTupleExpr : AstExpression
    {
        public List<AstIdExpr> Names { get; set; }
        public List<AstExpression> Elements { get; set; }

        /// <summary>
        /// 'true' if (int, int), 'false' if (3, 54)
        /// </summary>
        public bool IsTypedTuple { get; set; }

        public bool IsFullyNamed => Names?.All(x => x != null) ?? false;

        public override string AAAName => nameof(AstTupleExpr);

        public AstTupleExpr(List<AstExpression> elements, ILocation location)
            : base(location)
        {
            this.Elements = elements;
        }

        public override AstStatement GetDeepCopy()
        {
             throw new NotImplementedException("Tuple deep copy not implemented");
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (Names.Contains(oldChild))
            {
                int index = Names.IndexOf(oldChild as AstIdExpr);
                Names[index] = newChild as AstIdExpr;
            }
            else if (Elements.Contains(oldChild))
            {
                int index = Elements.IndexOf(oldChild as AstExpression);
                Elements[index] = newChild as AstExpression;
            }
        }
    }
}
