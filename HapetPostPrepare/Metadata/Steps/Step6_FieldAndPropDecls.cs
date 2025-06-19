using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
using System;
using System.Linq;

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
                    InternalVarPP(decl, cls.SubScope);
                }
            }
            else if (stmt is AstStructDecl str)
            {
                // infer fields at first
                foreach (var decl in str.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl).ToList())
                {
                    // field 
                    InternalVarPP(decl, str.SubScope);
                }
            }
            else if (stmt is AstGenericDecl gen)
            {
                // infer fields at first
                foreach (var decl in gen.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl).ToList())
                {
                    // field 
                    InternalVarPP(decl, gen.SubScope);
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

            void InternalVarPP(AstVarDecl decl, Scope parentSubScope)
            {
                // p prop generics here
                if (decl is AstPropertyDecl prop)
                    _ = PostPrepareMetadataGenerics(prop);

                // mute all inference errors for var type of property. 
                // if has to be errored somewhere else
                var savedMute = inInfo.MuteErrors;
                if (decl.IsPropertyField)
                    inInfo.MuteErrors = true;
                PostPrepareExprInference(decl.Type, inInfo, ref outInfo);
                if (decl.IsPropertyField)
                    inInfo.MuteErrors = savedMute;

                // do not allow static fields/props with gen types
                if (HasAnyGenericTypes([decl.Type]))
                {
                    var c1 = decl.SpecialKeys.Contains(TokenType.KwStatic);
                    var c2 = decl.IsExactly<AstVarDecl>() || 
                        (decl.IsExactly<AstPropertyDecl>() && (decl as AstPropertyDecl).GetBlock == null && (decl as AstPropertyDecl).HasGet);
                    if (c1 && c2)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Type, 
                            [], ErrorCode.Get(CTEN.StaticPolyField));
                }

                // inferencing additional data
                if (decl.Name.AdditionalData != null)
                    PostPrepareExprInference(decl.Name.AdditionalData, inInfo, ref outInfo);

                // define in scope
                parentSubScope.DefineDeclSymbol(decl.Name.GetCopy(), decl);
            }
        }
    }
}
