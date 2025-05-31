using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Types;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void HandleBasicTypes(AstDeclaration decl, AstIdExpr idExpr)
        {
            // all basic types are structs
            if (decl is not AstStructDecl structDecl)
                return;

            // special handle for string type
            if (decl.Name is AstIdExpr id && id.Name == "System.String")
            {
                // set the decl to the string type
                HapetType.CurrentTypeContext.StringTypeInstance.Declaration = structDecl;
                idExpr.OutType = HapetType.CurrentTypeContext.StringTypeInstance;
            }
            // special handle for array type
            else if (decl.Name is AstIdGenericExpr genId && genId.Name == "System.Array")
            {
                var targetType = genId.GenericRealTypes[0].OutType;
                if (targetType == null)
                    return;

                var arrT = HapetType.CurrentTypeContext.GetArrayType(targetType);
                // set decl if newly created
                if (arrT.Declaration == null)
                    arrT.Declaration = structDecl;
                idExpr.OutType = arrT;
            }
        }
    }
}
