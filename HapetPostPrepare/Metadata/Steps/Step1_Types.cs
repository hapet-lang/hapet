using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Scoping;
using HapetFrontend.Helpers;
using HapetFrontend.Types;
namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataTypes(AstStatement stmt, bool needSerialize = false)
        {
            // just skip allowed statements
            if (stmt is AstUsingStmt)
            {
                return;
            }

            if (stmt is not AstDeclaration decl)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, stmt, [], ErrorCode.Get(CTEN.StmtExpectedToBeDecl));
                return;
            }

            string newName;
            if (decl is AstClassDecl classDecl)
            {
                if (decl.IsNestedDecl)
                    // we need a pure decl name because it is nested
                    newName = $"{classDecl.Name.Name}";
                else
                    // creating a new class name with namespace
                    newName = $"{_currentSourceFile.Namespace}.{classDecl.Name.Name}";
                AllClassesMetadata.Add(classDecl);

                if (needSerialize)
                    _serializeClassesMetadata.Add(classDecl);
            }
            else if (decl is AstStructDecl structDecl)
            {
                // creating a new struct name with namespace
                newName = $"{_currentSourceFile.Namespace}.{structDecl.Name.Name}";
                AllStructsMetadata.Add(structDecl);

                if (needSerialize)
                    _serializeStructsMetadata.Add(structDecl);
            }
            else if (decl is AstEnumDecl enumDecl)
            {
                // creating a new enum name with namespace
                newName = $"{_currentSourceFile.Namespace}.{enumDecl.Name.Name}";
                AllEnumsMetadata.Add(enumDecl);

                if (needSerialize)
                    _serializeEnumsMetadata.Add(enumDecl);
            }
            else if (decl is AstDelegateDecl delegateDecl)
            {
                // creating a new delegate name with namespace
                newName = $"{_currentSourceFile.Namespace}.{delegateDecl.Name.Name}";
                AllDelegatesMetadata.Add(delegateDecl);

                if (needSerialize)
                    _serializeDelegatesMetadata.Add(delegateDecl);
            }
            else
            {
                // TODO:
                // no need to be angry - check it while parsing, not PP
                // _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name, [], ErrorCode.Get(CTEN.DeclNotAllowedInNamespace));
                return;
            }

            // TODO: check for partial :)
            decl.Name = decl.Name.GetCopy(newName);
            var smbl = _currentSourceFile.NamespaceScope.GetSymbol(decl.Name);
            // TODO: better error like where is the first decl?
            if (smbl != null)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name, [_currentSourceFile.Namespace], ErrorCode.Get(CTEN.NamespaceAlreadyContains));
            }
            else
            {
                _currentSourceFile.NamespaceScope.DefineDeclSymbol(decl.Name, decl);
                PostPrepareAliases(decl.Name, _currentSourceFile.NamespaceScope, decl);
            }
        }
    }
}
