using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Scoping;
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
                    var wasGeneric = InternalVarPP(decl, cls.SubScope);
                    if (wasGeneric)
                    {
                        var removedDecls = RemovePropertyShiteFromDecl(cls.Declarations, decl as AstPropertyDecl);
                        cls.Declarations.Remove(decl);
                        foreach (var d in removedDecls)
                        {
                            if (!AllFunctionsMetadata.Contains(d))
                                continue;
                            AllFunctionsMetadata.Remove(d as AstFuncDecl);
                        }
                    }
                }
            }
            else if (stmt is AstStructDecl str)
            {
                // infer fields at first
                foreach (var decl in str.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl).ToList())
                {
                    // field 
                    var wasGeneric = InternalVarPP(decl, str.SubScope);
                    if (wasGeneric)
                    {
                        var removedDecls = RemovePropertyShiteFromDecl(str.Declarations, decl as AstPropertyDecl);
                        str.Declarations.Remove(decl);
                        foreach (var d in removedDecls)
                        {
                            if (!AllFunctionsMetadata.Contains(d))
                                continue;
                            AllFunctionsMetadata.Remove(d as AstFuncDecl);
                        }
                    }
                }
            }
            else if (stmt is AstEnumDecl enm)
            {
                // infer fields at first
                foreach (var decl in enm.Declarations.ToList())
                {
                    // field 
                    InternalVarPP(decl, enm.SubScope);
                }
            }

            bool InternalVarPP(AstVarDecl decl, Scope parentSubScope)
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

                    realProp = decl;
                }

                if (realProp.Type.OutType is ClassType)
                {
                    // the var is actually a pointer to the class
                    var astPtr = new AstPointerExpr(realProp.Type, false, realProp.Type.Location);
                    astPtr.Scope = realProp.Type.Scope;
                    realProp.Type = new AstNestedExpr(astPtr, null, realProp.Type.Location) { OutType = astPtr.OutType };
                    PostPrepareExprInference(realProp.Type, inInfo, ref outInfo);
                }

                // define in scope
                // if it is public field - it should be visible in the scope in which var's class is
                string nameAddition = "";
                if (decl.Name.AdditionalData != null)
                {
                    PostPrepareExprInference(decl.Name.AdditionalData, inInfo, ref outInfo);
                    nameAddition = (decl.Name.AdditionalData.OutType as ClassType).Declaration.Name.Name;
                    nameAddition += '.';
                }
                parentSubScope.DefineDeclSymbol($"{nameAddition}{decl.Name.Name}", decl);
                decl.Name = decl.Name.GetCopy($"{nameAddition}{decl.Name.Name}");

                return itWasPureGenericProp;
            }
        }
    }
}
