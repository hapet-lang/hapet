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
                if (decl is AstPropertyDecl prop)
                    itWasPureGenericProp = PostPrepareMetadataGenerics(prop);

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
                }

                // define in scope
                // if it is public field - it should be visible in the scope in which var's class is
                parentSubScope.DefineDeclSymbol(decl.Name.GetCopy(), decl);

                return itWasPureGenericProp;
            }
        }
    }
}
