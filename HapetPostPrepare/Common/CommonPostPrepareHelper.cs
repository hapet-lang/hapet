using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;
using HapetFrontend.Types;
using HapetFrontend.Scoping;
using HapetFrontend.Ast.Expressions;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void PostPrepareAliases(AstIdExpr typeName, Scope scope, AstDeclaration decl)
        {
            // kostyl to create aliases :)
            if (typeName.Name == "System.Object")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("object"), decl);
            }
            else if (typeName.Name == "System.String")
            {
                decl.Type.OutType = StringType.GetInstance(decl as AstStructDecl);
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("string"), decl);
            }
            else if (typeName.Name == "System.Array")
            {
                decl.Type.OutType = new StructType(decl as AstStructDecl);
            }
        }
    }
}
