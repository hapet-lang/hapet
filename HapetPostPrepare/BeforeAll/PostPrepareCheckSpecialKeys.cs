using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Extensions;
using HapetFrontend.Parsing;

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
                RemoveIfMetadataDeclaration(decl);
                CheckSpecialKeys(decl);
            }
        }

        private void PostPrepareStructSpecialKeys(AstStructDecl structDecl)
        {
            foreach (var decl in structDecl.Declarations)
            {
                RemoveIfMetadataDeclaration(decl);
                CheckSpecialKeys(decl);
            }
        }

        private void RemoveIfMetadataDeclaration(AstDeclaration decl)
        {
            // this shite is done because .mpt abstract/virtual funcs
            // are serialized with both override and virtual/abstract keys
            // we need to remove virtual/abstract
            if (decl.IsImported && decl.SpecialKeys.Contains(TokenType.KwOverride) &&
                (decl.SpecialKeys.Contains(TokenType.KwVirtual) || decl.SpecialKeys.Contains(TokenType.KwAbstract)))
            {
                decl.SpecialKeys.Remove(TokenType.KwVirtual);
                decl.SpecialKeys.Remove(TokenType.KwAbstract);
            }
        }
    }
}
