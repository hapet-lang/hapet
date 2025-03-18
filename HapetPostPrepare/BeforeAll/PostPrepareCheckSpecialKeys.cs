using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareSpecialKeys()
        {
            foreach (var (path, file) in _compiler.GetFiles())
            {
                _currentSourceFile = file;
                foreach (var stmt in file.Statements)
                {
                    PostPrepareDeclSpecialKeys(stmt as AstDeclaration);
                }
            }
        }

        private void PostPrepareDeclSpecialKeys(AstDeclaration stmt)
        {
            CheckSpecialKeys(stmt);
            if (stmt is AstClassDecl classDecl)
            {
                PostPrepareClassSpecialKeys(classDecl);
            }
            else if (stmt is AstStructDecl structDecl)
            {
                PostPrepareStructSpecialKeys(structDecl);
            }
            // TODO: also check nested func' declarations 
        }

        private void PostPrepareClassSpecialKeys(AstClassDecl classDecl)
        {
            _currentClass = classDecl;

            foreach (var decl in classDecl.Declarations)
            {
                CheckSpecialKeys(decl);
            }
        }

        private void PostPrepareStructSpecialKeys(AstStructDecl structDecl)
        {
            foreach (var decl in structDecl.Declarations)
            {
                CheckSpecialKeys(decl);
            }
        }
    }
}
