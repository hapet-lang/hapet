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
                _postPreparer._currentSourceFile = cls.SourceFile;
                CreateRequiredInDecl(cls);
            }
            foreach (var str in _postPreparer.AllStructsMetadata)
            {
                _postPreparer._currentSourceFile = str.SourceFile;
                CreateRequiredInDecl(str);
            }
        }

        public void CreateRequiredInDecl(AstDeclaration decl)
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
            }
        }
    }
}
