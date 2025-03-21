using HapetFrontend.Ast.Declarations;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        /// names of some funcs/vars has to be the same as in <see cref="RenameFromGenericToRealType"/>
        private void PostPrepareMetaClassMethods()
        {
            foreach (var (path, file) in _compiler.GetFiles())
            {
                // skip not imported files
                if (!file.IsImported)
                    continue;

                _currentSourceFile = file;

                foreach (var stmt in file.Statements)
                {
                    if (stmt is AstClassDecl classDecl)
                    {
                        PostPrepareClassMethodsInternal(classDecl, true);
                    }
                    else if (stmt is AstStructDecl structDecl)
                    {
                        PostPrepareStructMethodsInternal(structDecl, true);
                    }
                }
            }
        }
    }
}
