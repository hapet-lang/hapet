using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;
using HapetFrontend.Types;
using HapetFrontend.Scoping;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public static void PostPrepareAliases(string typeName, Scope scope, AstDeclaration decl)
        {
            // kostyl to create aliases :)
            if (typeName == "System.Object")
            {
                scope.DefineDeclSymbol("System.object", decl);
            }
            else if (typeName == "System.String")
            {
                decl.Type.OutType = StringType.GetInstance(decl as AstStructDecl);
                scope.DefineDeclSymbol("System.string", decl);
            }
            else if (typeName == "System.Array")
            {
                decl.Type.OutType = new StructType(decl as AstStructDecl);
            }
        }
    }
}
