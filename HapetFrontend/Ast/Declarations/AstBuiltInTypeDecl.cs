using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
    /// <summary>
    /// Ast class for built in types (use it only in <see cref="HapetFrontend.Scoping.Scope"/>)
    /// </summary>
    public class AstBuiltInTypeDecl : AstDeclaration
    {
        public override string AAAName => nameof(AstBuiltInTypeDecl);

        public AstBuiltInTypeDecl(HapetType tp, string doc = "", ILocation Location = null) : base(null, doc, Location)
        {
            Type = new AstIdExpr(tp.TypeName, Location);
            Type.OutType = tp;
        }
    }
}
