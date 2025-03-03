using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;

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
                foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                {
                    // field or property
                    InternalVarPP(decl);
                }
            }
            else if (stmt is AstStructDecl str)
            {
                // infer fields at first
                foreach (var decl in str.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                {
                    // field 
                    InternalVarPP(decl);
                }
            }
            else if (stmt is AstEnumDecl enm)
            {
                // infer fields at first
                foreach (var decl in enm.Declarations)
                {
                    // field 
                    InternalVarPP(decl);
                }
            }

            void InternalVarPP(AstVarDecl decl)
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
                    decl.Type = astPtr;
                    PostPrepareExprInference(decl.Type, inInfo, ref outInfo);
                }
            }
        }
    }
}
