using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
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
                    if (stmt is not AstDeclaration decl)
                        continue;
                    PostPrepareDeclSpecialKeys(decl);
                }
            }
        }

        private void PostPrepareDeclSpecialKeys(AstDeclaration stmt)
        {
            _currentParentStack.AddParent(stmt);

            SpecialKeysHelper.CheckSpecialKeys(stmt, _compiler.MessageHandler, _currentSourceFile);
            if (stmt is AstClassDecl classDecl)
            {
                PostPrepareClassSpecialKeys(classDecl);
            }
            else if (stmt is AstStructDecl structDecl)
            {
                PostPrepareStructSpecialKeys(structDecl);
            }
            // also check nested func' declarations 
            else if (stmt is AstFuncDecl funcDecl)
            {
                PostPrepareFuncSpecialKeys(funcDecl);
            }

            _currentParentStack.RemoveParent();
        }

        private void PostPrepareClassSpecialKeys(AstClassDecl classDecl)
        {
            foreach (var decl in classDecl.Declarations)
            {
                RemoveIfMetadataDeclaration(decl);
                SpecialKeysHelper.CheckSpecialKeys(decl, _compiler.MessageHandler, _currentSourceFile);

                if (decl is AstClassDecl || decl is AstStructDecl)
                    PostPrepareDeclSpecialKeys(decl);
            }
        }

        private void PostPrepareStructSpecialKeys(AstStructDecl structDecl)
        {
            foreach (var decl in structDecl.Declarations)
            {
                RemoveIfMetadataDeclaration(decl);
                SpecialKeysHelper.CheckSpecialKeys(decl, _compiler.MessageHandler, _currentSourceFile);

                if (decl is AstClassDecl || decl is AstStructDecl)
                    PostPrepareDeclSpecialKeys(decl);
            }
        }

        private void PostPrepareFuncSpecialKeys(AstFuncDecl funcDecl)
        {
            // skip funcs without body
            if (funcDecl.Body == null)
                return;

            foreach (var stmt in funcDecl.Body.Statements)
            {
                // skip non decls
                if (stmt is not AstDeclaration decl)
                    continue;

                RemoveIfMetadataDeclaration(decl);
                SpecialKeysHelper.CheckSpecialKeys(decl, _compiler.MessageHandler, _currentSourceFile);

                if (decl is AstFuncDecl)
                    PostPrepareDeclSpecialKeys(decl);
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
