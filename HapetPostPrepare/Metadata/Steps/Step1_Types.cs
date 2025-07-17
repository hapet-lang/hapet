using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Scoping;
using HapetFrontend.Helpers;
using HapetFrontend.Types;
using HapetFrontend.Parsing;
using HapetFrontend.Enums;
namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataTypes(AstStatement stmt, bool needSerialize = false)
        {
            // just skip allowed statements
            if (stmt is AstUsingStmt || stmt is AstFuncDecl)
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
                {
                    // creating a new class name with namespace
                    newName = $"{_currentSourceFile.Namespace}.{classDecl.Name.Name}";
                    AllClassesMetadata.Add(classDecl);
                }

                if (needSerialize)
                    _serializeClassesMetadata.Add(classDecl);
            }
            else if (decl is AstStructDecl structDecl)
            {
                if (decl.IsNestedDecl)
                    // we need a pure decl name because it is nested
                    newName = $"{structDecl.Name.Name}";
                else
                {
                    // creating a new struct name with namespace
                    newName = $"{_currentSourceFile.Namespace}.{structDecl.Name.Name}";
                    AllStructsMetadata.Add(structDecl);
                }

                if (needSerialize)
                    _serializeStructsMetadata.Add(structDecl);
            }
            else if (decl is AstEnumDecl enumDecl)
            {
                if (decl.IsNestedDecl)
                    // we need a pure decl name because it is nested
                    newName = $"{enumDecl.Name.Name}";
                else
                {
                    // creating a new enum name with namespace
                    newName = $"{_currentSourceFile.Namespace}.{enumDecl.Name.Name}";
                    AllEnumsMetadata.Add(enumDecl);
                }

                if (needSerialize)
                    _serializeEnumsMetadata.Add(enumDecl);
            }
            else if (decl is AstDelegateDecl delegateDecl)
            {
                if (decl.IsNestedDecl)
                    // we need a pure decl name because it is nested
                    newName = $"{delegateDecl.Name.Name}";
                else
                {
                    // creating a new delegate name with namespace
                    newName = $"{_currentSourceFile.Namespace}.{delegateDecl.Name.Name}";
                    AllDelegatesMetadata.Add(delegateDecl);
                }

                if (needSerialize)
                    _serializeDelegatesMetadata.Add(delegateDecl);
            }
            else
            {
                // should be errored in frontend
                return;
            }

            // TODO: check for partial :)
            decl.Name = decl.Name.GetCopy(newName);
            // if the decl is not nested - declare it in namespace scope
            Scope scopeToDefine;
            if (decl.IsNestedDecl) scopeToDefine = decl.ParentDecl.SubScope;
            else scopeToDefine = _currentSourceFile.NamespaceScope;

            var smbl = scopeToDefine.GetSymbol(decl.Name);
            // TODO: better error like where is the first decl?
            if (smbl != null)
            {
                // check for partials
                if (smbl is DeclSymbol ds && ds.Decl.SpecialKeys.Contains(TokenType.KwPartial) && decl.SpecialKeys.Contains(TokenType.KwPartial))
                {
                    HandlePartialDeclarations(ds.Decl, decl);
                }
                else
                {
                    // TODO: different error when nested
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name, [_currentSourceFile.Namespace], ErrorCode.Get(CTEN.NamespaceAlreadyContains));
                }
            }
            else
            {
                scopeToDefine.DefineDeclSymbol(decl.Name, decl);
                PostPrepareAliases(decl.Name, scopeToDefine, decl);
            }
        }

        private void HandlePartialDeclarations(AstDeclaration alreadyDeclared, AstDeclaration newOne)
        {
            alreadyDeclared.GetInheritedTypes().AddRange(newOne.GetInheritedTypes());
            // skip here non-default funcs and static ctor field
            var declsToAdd = newOne.GetDeclarations().Where(x => !(
                (x is AstFuncDecl fnc && fnc.ClassFunctionType != ClassFunctionType.Default) || 
                (x is AstVarDecl vd && vd.IsStaticCtorField)));
            // change parent and subscope
            foreach (var d in declsToAdd)
            {
                d.ContainingParent = alreadyDeclared;
                d.Scope = alreadyDeclared.SubScope;
            }

            // TODO: handle stors, ctors, ini, error on double dtors/stors

            alreadyDeclared.GetDeclarations().AddRange(declsToAdd);

            // remove from inference
            if (newOne is AstClassDecl cls)
            {
                AllClassesMetadata.RemoveAll(x => x == cls);
                _serializeClassesMetadata.RemoveAll(x => x == cls);
            }
            else if (newOne is AstStructDecl str)
            {
                AllStructsMetadata.RemoveAll(x => x == str);
                _serializeStructsMetadata.RemoveAll(x => x == str);
            }
        }
    }
}
