using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetPostPrepare.Entities;
using System;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataAttributes(AstStatement stmt)
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            if (stmt is AstFuncDecl fnc)
            {
                // inferencing attrs
                foreach (var a in fnc.Attributes)
                {
                    PostPrepareAttributeStmtInference(a, inInfo, ref outInfo);
                }
                // inferencing params attrs
                foreach (var p in fnc.Parameters)
                {
                    // inferencing attrs
                    foreach (var a in p.Attributes)
                    {
                        PostPrepareAttributeStmtInference(a, inInfo, ref outInfo);
                    }
                }
            }
            else if (stmt is AstClassDecl cls)
            {
                // infer fields and props attibutes
                foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                {
                    // inferencing attrs
                    foreach (var a in decl.Attributes)
                    {
                        PostPrepareAttributeStmtInference(a, inInfo, ref outInfo);
                    }
                }
                // inferencing attrs
                foreach (var a in cls.Attributes)
                {
                    PostPrepareAttributeStmtInference(a, inInfo, ref outInfo);
                }
            }
            else if (stmt is AstStructDecl str)
            {
                // infer fields and props attibutes
                foreach (var decl in str.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                {
                    // inferencing attrs
                    foreach (var a in decl.Attributes)
                    {
                        PostPrepareAttributeStmtInference(a, inInfo, ref outInfo);
                    }
                }
                // inferencing attrs
                foreach (var a in str.Attributes)
                {
                    PostPrepareAttributeStmtInference(a, inInfo, ref outInfo);
                }
            }
            else if (stmt is AstEnumDecl enm)
            {
                // inferencing attrs
                foreach (var a in enm.Attributes)
                {
                    PostPrepareAttributeStmtInference(a, inInfo, ref outInfo);
                }
            }
            else if (stmt is AstDelegateDecl del)
            {
                // inferencing attrs
                foreach (var a in del.Attributes)
                {
                    PostPrepareAttributeStmtInference(a, inInfo, ref outInfo);
                }
                // inferencing params attrs
                foreach (var p in del.Parameters)
                {
                    // inferencing attrs
                    foreach (var a in p.Attributes)
                    {
                        PostPrepareAttributeStmtInference(a, inInfo, ref outInfo);
                    }
                }
            }
        }
    }
}
