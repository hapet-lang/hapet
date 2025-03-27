using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;
using HapetPostPrepare.Entities;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataNestedTypes(AstStatement stmt)
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            if (stmt is AstClassDecl cls)
            {
                foreach (var decl in cls.Declarations.Where(x => x is not AstFuncDecl && x is not AstVarDecl).ToList())
                {
                    PPNested(decl);
                }
            }
            else if (stmt is AstStructDecl str)
            {
                foreach (var decl in str.Declarations.Where(x => x is not AstFuncDecl && x is not AstVarDecl).ToList())
                {
                    PPNested(decl);
                }
            }

            void PPNested(AstDeclaration decl)
            {
                PostPrepareStatementUpToCurrentStep(decl);
            }
        }
    }
}
