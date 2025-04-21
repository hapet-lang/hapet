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

        public bool IsFullyNamed => Names.All(x => x != null);

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
    }
}
