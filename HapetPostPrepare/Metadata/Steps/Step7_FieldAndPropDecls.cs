using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
using System;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataTypeFieldDecls(AstStatement stmt)
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            if (stmt is AstClassDecl cls)
            {
                // infer fields and props at first
                foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl).ToList())
                {
                    // field or property
                    var wasGeneric = InternalVarPP(decl);
                    if (wasGeneric)
                    {
                        RemovePropertyShiteFromDecl(cls.Declarations, decl as AstPropertyDecl);
                        cls.Declarations.Remove(decl);
                    }
                }
            }
            else if (stmt is AstStructDecl str)
            {
                // infer fields at first
                foreach (var decl in str.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl).ToList())
                {
                    // field 
                    var wasGeneric = InternalVarPP(decl);
                    if (wasGeneric)
                    {
                        RemovePropertyShiteFromDecl(str.Declarations, decl as AstPropertyDecl);
                        str.Declarations.Remove(decl);
                    }
                }
            }
            else if (stmt is AstEnumDecl enm)
            {
                // infer fields at first
                foreach (var decl in enm.Declarations.ToList())
                {
                    // field 
                    InternalVarPP(decl);
                }
            }

            bool InternalVarPP(AstVarDecl decl)
            {
                // p prop generics here
                bool itWasPureGenericProp = false;
                AstDeclaration realProp = null;
                if (decl is AstPropertyDecl prop)
                    itWasPureGenericProp = PostPrepareMetadataGenerics(prop, out realProp);

                // do not infer pure generic funcs
                if (!itWasPureGenericProp)
                {
                    // mute all inference errors for var type of property. 
                    // if has to be errored somewhere else
                    var savedMute = inInfo.MuteErrors;
                    if (decl.IsPropertyField)
                        inInfo.MuteErrors = true;
                    PostPrepareExprInference(decl.Type, inInfo, ref outInfo);
                    if (decl.IsPropertyField)
                        inInfo.MuteErrors = savedMute;

                    if (decl.Type.OutType is ClassType)
                    {
                        // the var is actually a pointer to the class
                        var astPtr = new AstPointerExpr(decl.Type, false, decl.Type.Location);
                        astPtr.Scope = decl.Type.Scope;
                        decl.Type = new AstNestedExpr(astPtr, null, decl.Type.Location) { OutType = astPtr.OutType };
                        PostPrepareExprInference(decl.Type, inInfo, ref outInfo);
                    }
                }
                else
                {
                    if (realProp.Type.OutType is ClassType)
                    {
                        // the var is actually a pointer to the class
                        var astPtr = new AstPointerExpr(realProp.Type, false, realProp.Type.Location);
                        astPtr.Scope = realProp.Type.Scope;
                        realProp.Type = new AstNestedExpr(astPtr, null, realProp.Type.Location) { OutType = astPtr.OutType };
                        PostPrepareExprInference(realProp.Type, inInfo, ref outInfo);
                    }
                }

                return itWasPureGenericProp;
            }
        }
    }
}
