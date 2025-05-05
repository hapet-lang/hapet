using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
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
                foreach (var decl in cls.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl).ToList())
                {
                    PPFunc(decl);
                }
            }
            else if (stmt is AstStructDecl str)
            {
                foreach (var decl in str.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl).ToList())
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
                AllFunctionsMetadata.Add(func);

                // p func generics here
                bool _ = PostPrepareMetadataGenerics(func);

                inInfo.ForMetadata = true;
                PostPrepareFunctionInference(func, inInfo, ref outInfo);
                inInfo.ForMetadata = false;

                // if func serialization required
                if (needSerialize)
                {
                    _serializeFunctionsMetadata.Add(func);
                }

                // set that the function is imported from another assembly
                func.IsImported = isImported;

                if (func.Body != null && func.Body.Statements.Count > 0)
                    // check for nested funcs - prepare them
                    foreach (var nestedFunc in func.Body.Statements.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl).ToList())
                    {
                        PostPrepareMetadataFunctions(nestedFunc, false, isImported);
                    }
            }
        }
    }
}
