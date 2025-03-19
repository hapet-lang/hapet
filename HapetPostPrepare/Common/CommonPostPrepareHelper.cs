using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;
using HapetFrontend.Types;
using HapetFrontend.Scoping;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void PostPrepareAliases(string typeName, Scope scope, AstDeclaration decl)
        {
            // kostyl to create aliases :)
            if (typeName == "System.Object")
            {
                _compiler.GlobalScope.DefineDeclSymbol("object", decl);
            }
            else if (typeName == "System.String")
            {
                decl.Type.OutType = StringType.GetInstance(decl as AstStructDecl);
                _compiler.GlobalScope.DefineDeclSymbol("string", decl);
            }
            else if (typeName == "System.Array")
            {
                decl.Type.OutType = new StructType(decl as AstStructDecl);
            }
        }
    }
}
