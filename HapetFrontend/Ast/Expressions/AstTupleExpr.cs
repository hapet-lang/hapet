using HapetFrontend.Ast.Declarations;

namespace HapetFrontend.Ast.Expressions
{
    public class AstTupleExpr : AstExpression
    {
        public bool IsFullyNamed => Types.All(t => t.Name != null);
        public List<AstExpression> Values { get; set; }
        public List<AstParamDecl> Types { get; set; }

        public override string AAAName => nameof(AstTupleExpr);

        public AstTupleExpr(List<AstParamDecl> values, ILocation Location)
            : base(Location)
        {
            this.Types = values;
            this.Values = Types.Select(t => t.Type).ToList();
        }
        public AstTupleExpr(List<AstExpression> values, ILocation Location)
            : base(Location)
        {
            this.Types = values.Select(v => new AstParamDecl(v, null, null, "", v.Location)).ToList();
            this.Values = Types.Select(t => t.Type).ToList();
        }
    }
}
