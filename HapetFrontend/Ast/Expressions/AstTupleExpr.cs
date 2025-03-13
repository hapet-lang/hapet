using HapetFrontend.Ast.Declarations;

namespace HapetFrontend.Ast.Expressions
{
    public class AstTupleExpr : AstExpression
    {
        public bool IsFullyNamed => Types.All(t => t.Name != null);
        public List<AstExpression> Values { get; set; }
        public List<AstParamDecl> Types { get; set; }

        public override string AAAName => nameof(AstTupleExpr);

        public AstTupleExpr(List<AstParamDecl> values, ILocation location)
            : base(location)
        {
            this.Types = values;
            this.Values = Types.Select(t => t.Type as AstExpression).ToList();
        }
        public AstTupleExpr(List<AstNestedExpr> values, ILocation location)
            : base(location)
        {
            this.Types = values.Select(v => new AstParamDecl(v, null, null, "", v.Location)).ToList();
            this.Values = Types.Select(t => t.Type as AstExpression).ToList();
        }

        public override AstStatement GetDeepCopy()
        {
             throw new NotImplementedException("Tuple deep copy not implemented");
        }
    }
}
