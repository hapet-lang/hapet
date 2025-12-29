using HapetFrontend.Ast.Expressions;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Statements
{
    // should be removed from file before pp and code gen!!!
    public class AstNamespaceStmt : AstStatement
    {
        /// <summary>
        /// Namespace name (the name after 'namespace' word)
        /// </summary>
        public AstNestedExpr NameExpression { get; set; }

        public override string AAAName => nameof(AstNamespaceStmt);

        public AstNamespaceStmt(AstNestedExpr name, ILocation location = null) : base(location)
        {
            NameExpression = name;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstNamespaceStmt(
                NameExpression.GetDeepCopy() as AstNestedExpr,
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
            if (NameExpression == oldChild)
                NameExpression = newChild as AstNestedExpr;
        }
    }
}
