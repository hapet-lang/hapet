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
                // PostPrepareStatementUpToCurrentStep(decl);
                PostPrepareMetadataTypes(decl, false);
            }
        }

        private void PostPrepareMetadataNestedTypesInside(AstStatement stmt)
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
                if (decl is AstClassDecl classDecl)
                {
                    AllClassesMetadata.Add(classDecl);
                }
                else if (decl is AstStructDecl structDecl)
                {
                    AllStructsMetadata.Add(structDecl);
                }
                else if (decl is AstEnumDecl enumDecl)
                {
                    AllEnumsMetadata.Add(enumDecl);
                }
                else if (decl is AstDelegateDecl delegateDecl)
                {
                    AllDelegatesMetadata.Add(delegateDecl);
                }

                // PostPrepareMetadataTypes(decl, false);
                PostPrepareStatementUpToCurrentStep(decl, true);
            }
        }
    }
}
