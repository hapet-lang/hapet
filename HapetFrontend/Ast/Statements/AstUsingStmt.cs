using HapetFrontend.Ast.Expressions;
using System.Xml.Linq;

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

        public AstUsingStmt(AstNestedExpr ns, ILocation location = null) : base(location)
        {
            Namespace = ns;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstUsingStmt(
                Namespace.GetDeepCopy() as AstNestedExpr,
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
            if (Namespace == oldChild)
                Namespace = newChild as AstNestedExpr;
        }
    }
}
