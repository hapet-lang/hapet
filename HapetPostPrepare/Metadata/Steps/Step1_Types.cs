using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Errors;
namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataTypes(AstStatement stmt)
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
                _currentClass = classDecl;

                // creating a new class name with namespace
                newName = $"{_currentSourceFile.Namespace}.{classDecl.Name.Name}";
                AllClassesMetadata.Add(classDecl);
                _serializeClassesMetadata.Add(classDecl);
            }
            else if (decl is AstStructDecl structDecl)
            {
                // creating a new struct name with namespace
                newName = $"{_currentSourceFile.Namespace}.{structDecl.Name.Name}";
                AllStructsMetadata.Add(structDecl);
                _serializeStructsMetadata.Add(structDecl);
            }
            else if (decl is AstEnumDecl enumDecl)
            {
                // creating a new enum name with namespace
                newName = $"{_currentSourceFile.Namespace}.{enumDecl.Name.Name}";
                AllEnumsMetadata.Add(enumDecl);
                _serializeEnumsMetadata.Add(enumDecl);
            }
            else if (decl is AstDelegateDecl delegateDecl)
            {
                // creating a new delegate name with namespace
                newName = $"{_currentSourceFile.Namespace}.{delegateDecl.Name.Name}";
                AllDelegatesMetadata.Add(delegateDecl);
                _serializeDelegatesMetadata.Add(delegateDecl);
            }
            else
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name, [], ErrorCode.Get(CTEN.DeclNotAllowedInNamespace));
                return;
            }

            // TODO: check for partial :)
            decl.Name = decl.Name.GetCopy(newName);
            var smbl = _currentSourceFile.NamespaceScope.GetSymbol(decl.Name.Name);
            // TODO: better error like where is the first decl?
            if (smbl != null)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name, [_currentSourceFile.Namespace], ErrorCode.Get(CTEN.NamespaceAlreadyContains));
            }
            else
            {
                _currentSourceFile.NamespaceScope.DefineDeclSymbol(decl.Name.Name, decl);

                PostPrepareAliases(newName, _currentSourceFile.NamespaceScope, decl);
            }
        }
    }
}
