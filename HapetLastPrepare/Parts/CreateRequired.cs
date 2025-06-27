using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Helpers;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        private void CreateRequired()
        {
            foreach (var cls in _postPreparer.AllClassesMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(cls))
                    continue;
                _postPreparer._currentSourceFile = cls.SourceFile;
                CreateRequiredInDecl(cls);
            }
            foreach (var str in _postPreparer.AllStructsMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(str))
                    continue;
                _postPreparer._currentSourceFile = str.SourceFile;
                CreateRequiredInDecl(str);
            }
        }

        private void CreateRequiredInDecl(AstDeclaration decl)
        {
            List<AstDeclaration> decls;
            if (decl is AstClassDecl clsDecl)
                decls = clsDecl.Declarations.ToList();
            else if (decl is AstStructDecl strDecl)
                decls = strDecl.Declarations.ToList();
            else
                return;

            _postPreparer._currentSourceFile = decl.SourceFile;
            foreach (var d in decls)
            {
                if (d is AstPropertyDecl pd)
                    CreateGetSetForProps(pd);
                else if (d is AstVarDecl vd)
                    CreateGetSetForStatic(vd);
            }
        }
    }
}
