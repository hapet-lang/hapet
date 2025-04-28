using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public const string VA_LIST_NAME = "System.Runtime.InteropServices.VaList"; // WARN: hardcock
        public HapetType VaListType { get; set; }

        private void PostPrepareMetadataEmbededTypes()
        {
            RegisterVaList();
        }

        // https://llvm.org/docs/LangRef.html#variable-argument-handling-intrinsics
        private void RegisterVaList()
        {
            // decls depending on platform
            List<AstDeclaration> decls;
            // TODO: WARN:!!! not linux64 but UNIX 86_64. linux != unix 
            if (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform == HapetFrontend.TargetPlatform.Linux64)
            {
                decls = new List<AstDeclaration>()
                {
                    new AstVarDecl(new AstNestedExpr(new AstIdExpr("int"), null), new AstIdExpr("hz1")),
                    new AstVarDecl(new AstNestedExpr(new AstIdExpr("int"), null), new AstIdExpr("hz2")),
                    new AstVarDecl(new AstNestedExpr(new AstPointerExpr(new AstIdExpr("byte")), null), new AstIdExpr("hz3")),
                    new AstVarDecl(new AstNestedExpr(new AstPointerExpr(new AstIdExpr("byte")), null), new AstIdExpr("hz4")),
                };
            }
            else
            {
                decls = new List<AstDeclaration>()
                {
                    new AstVarDecl(new AstNestedExpr(new AstPointerExpr(new AstIdExpr("byte")), null), new AstIdExpr("hz1")),
                };
            }

            // creating a new struct name with namespace
            var vaListName = new AstIdExpr(VA_LIST_NAME);
            var structDecl = new AstStructDecl(vaListName, decls);
            VaListType = structDecl.Type.OutType;

            // scoping 
            _compiler.GlobalScope.DefineSymbol(new DeclSymbol(vaListName, structDecl));
            SetScopeAndParent(structDecl, null, _compiler.GlobalScope);
            PostPrepareStructScoping(structDecl);

            AllStructsMetadata.Add(structDecl);
        }
    }
}
