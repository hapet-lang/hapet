using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    public class AstUsingStmt : AstStatement
    {
        /// <summary>
        /// The namespace to be used. Could be <see cref="AstNestedExpr"/>
        /// </summary>
        public AstNestedExpr Namespace { get; set; }

        // used to search symbols in it
        private string _flattenNamespace;
        public string FlattenNamespace
        {
            get
            {
                _flattenNamespace ??= Namespace.TryFlatten(null, null);
                return _flattenNamespace;
            }
        }

        public override string AAAName => nameof(AstUsingStmt);

        public AstUsingStmt(AstNestedExpr ns, ILocation Location = null) : base(Location)
        {
            Namespace = ns;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstUsingStmt(
                Namespace.GetDeepCopy() as AstNestedExpr,
                Location)
            {
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
