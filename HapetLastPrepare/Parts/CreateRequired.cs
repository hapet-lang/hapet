using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Helpers;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        private void CreateRequired()
        {
            List<AstDeclaration> declarations = new List<AstDeclaration>();
            declarations.AddRange(_postPreparer.AllClassesMetadata);
            declarations.AddRange(_postPreparer.AllStructsMetadata);
            foreach (var decl in declarations)
            {
                _postPreparer._currentSourceFile = decl.SourceFile;
                CreateRequiredInDecl(decl);
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
