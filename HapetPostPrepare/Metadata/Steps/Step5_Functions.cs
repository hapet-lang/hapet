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
        private void PostPrepareMetadataFunctions(AstStatement stmt, bool needSerialize = false, bool isImported = false, bool allowRemovance = false)
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
                bool itWasPureGenericFunc = PostPrepareMetadataGenerics(func, out var realFnc);

                // do not infer pure generic funcs
                if (!itWasPureGenericFunc)
                {
                    inInfo.ForMetadata = true;
                    PostPrepareFunctionInference(func, inInfo, ref outInfo);
                    inInfo.ForMetadata = false;
                }
                else
                {
                    // remove if allowed
                    if (allowRemovance)
                    {
                        // remove this shite from decls
                        if (stmt is AstClassDecl cls)
                        {
                            cls.Declarations.Add(realFnc);
                            cls.Declarations.Remove(func);
                        }
                        else if (stmt is AstStructDecl str)
                        {
                            str.Declarations.Add(realFnc);
                            str.Declarations.Remove(func);
                        }

                        // remove from inferencing
                        AllFunctionsMetadata.Remove(func);
                    }
                }

                // if func serialization required
                if (needSerialize)
                {
                    // if need serialize - check for generic shite - serialize only T-like funcs :)
                    if (!itWasPureGenericFunc)
                        _serializeFunctionsMetadata.Add(func);
                    else
                        _serializeFunctionsMetadata.Add(realFnc as AstFuncDecl);
                }

                if (isImported)
                    // set that the function is imported from another assembly
                    func.SpecialKeys.Add(TokenType.KwImported);
            }
        }
    }
}
