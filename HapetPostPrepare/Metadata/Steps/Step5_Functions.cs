using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Parsing;
using HapetPostPrepare.Entities;
using System;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataFunctions(AstStatement stmt, bool needSerialize = false, bool isImported = false)
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            if (stmt is AstClassDecl cls)
            {
                foreach (var decl in cls.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
                {
                    PPFunc(decl);
                }
            }
            else if (stmt is AstStructDecl str)
            {
                foreach (var decl in str.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
                {
                    PPFunc(decl);
                }
            }
            else if (stmt is AstFuncDecl func)
            {
                PPFunc(func);
            }

            void PPFunc(AstFuncDecl func)
            {
                inInfo.ForMetadata = true;
                PostPrepareFunctionInference(func, inInfo, ref outInfo);
                inInfo.ForMetadata = false;
                AllFunctionsMetadata.Add(func);

                if (needSerialize)
                    _serializeFunctionsMetadata.Add(func);

                if (isImported)
                    // set that the function is imported from another assembly
                    func.SpecialKeys.Add(TokenType.KwImported);
            }
        }
    }
}
