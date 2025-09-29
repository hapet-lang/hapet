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
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, stmt, [], ErrorCode.Get(CTEN.StmtExpectedToBeDecl));
                return;
            }

            if (decl is AstClassDecl classDecl)
            {
                if (!decl.IsNestedDecl)
                    AllClassesMetadata.Add(classDecl);

                if (needSerialize)
                    _serializeClassesMetadata.Add(classDecl);
            }
            else if (decl is AstStructDecl structDecl)
            {
                if (!decl.IsNestedDecl)
                    AllStructsMetadata.Add(structDecl);

                if (needSerialize)
                    _serializeStructsMetadata.Add(structDecl);
            }
            else if (decl is AstEnumDecl enumDecl)
            {
                if (!decl.IsNestedDecl)
                    AllEnumsMetadata.Add(enumDecl);

                if (needSerialize)
                    _serializeEnumsMetadata.Add(enumDecl);
            }
            else if (decl is AstDelegateDecl delegateDecl)
            {
                if (!decl.IsNestedDecl)
                    AllDelegatesMetadata.Add(delegateDecl);

                if (needSerialize)
                    _serializeDelegatesMetadata.Add(delegateDecl);
            }
            else
            {
                // should be errored in frontend
                return;
            }

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
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, decl.Name, [_currentSourceFile.Namespace], ErrorCode.Get(CTEN.NamespaceAlreadyContains));
                }
            }
            else
            {
                scopeToDefine.DefineDeclSymbol(decl.Name, decl);
                PostPrepareAliases(decl.Name, scopeToDefine, decl, $"{_currentSourceFile.Namespace}.{decl.Name.Name}");
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
                PostPrepareDeclScoping(d);
                // change parent of nested
                if (d.IsNestedDecl)
                    d.ParentDecl = alreadyDeclared;
            }

            // handle ini
            var newIni = newOne.GetDeclarations().FirstOrDefault(x => x is AstFuncDecl fd && fd.ClassFunctionType == ClassFunctionType.Initializer);
            var oldIni = alreadyDeclared.GetDeclarations().FirstOrDefault(x => x is AstFuncDecl fd && fd.ClassFunctionType == ClassFunctionType.Initializer);
            if (newIni is AstFuncDecl newIniF && oldIni is AstFuncDecl oldIniF)
            {
                // just add all statements from new decl to old
                foreach (var s in newIniF.Body.Statements)
                {
                    // skip last (? not sure) return stmt
                    if (s is AstReturnStmt)
                        continue;
                    oldIniF.Body.Statements.Add(s);
                }
            }

            // handle ctor
            var newCtors = newOne.GetDeclarations().Where(x => x is AstFuncDecl fd && fd.ClassFunctionType == ClassFunctionType.Ctor);
            var oldCtors = alreadyDeclared.GetDeclarations().Where(x => x is AstFuncDecl fd && fd.ClassFunctionType == ClassFunctionType.Ctor).ToArray();
            foreach (var ct in newCtors)
            {
                // skip synthetic shite
                if (ct.IsSyntheticDeclaration)
                    continue;
                
                ct.ContainingParent = alreadyDeclared;
                ct.Scope = alreadyDeclared.SubScope;
                PostPrepareDeclScoping(ct);
                alreadyDeclared.GetDeclarations().Add(ct);
            }

            // handle dtor
            var newDtor = newOne.GetDeclarations().FirstOrDefault(x => x is AstFuncDecl fd && fd.ClassFunctionType == ClassFunctionType.Dtor);
            var oldDtor = alreadyDeclared.GetDeclarations().FirstOrDefault(x => x is AstFuncDecl fd && fd.ClassFunctionType == ClassFunctionType.Dtor);
            if (newDtor is AstFuncDecl newDtorF && oldDtor is AstFuncDecl oldDtorF)
            {
                // just add all statements from new decl to old
                foreach (var s in newDtorF.Body.Statements)
                {
                    // skip last (? not sure) return stmt
                    if (s is AstReturnStmt)
                        continue;
                    oldDtorF.Body.Statements.Add(s);
                }

                // TODO: error if new and old are not synthetic at the same time
            }

            // handle stor
            var newStor = newOne.GetDeclarations().FirstOrDefault(x => x is AstFuncDecl fd && fd.ClassFunctionType == ClassFunctionType.StaticCtor);
            var oldStor = alreadyDeclared.GetDeclarations().FirstOrDefault(x => x is AstFuncDecl fd && fd.ClassFunctionType == ClassFunctionType.StaticCtor);
            if (newStor is AstFuncDecl newStorF && oldStor is AstFuncDecl oldStorF)
            {
                // getting all initers from stor block without storVar assign (skip last)
                var statementsNew = (newStorF.Body.Statements[0] as AstIfStmt).BodyTrue.Statements.SkipLast(1);
                // block with init statements in old stor
                var blockOld = (oldStorF.Body.Statements[0] as AstIfStmt).BodyTrue;
                // just add all statements from new decl to old
                foreach (var s in statementsNew)
                {
                    // skip last (? not sure) return stmt
                    if (s is AstReturnStmt)
                        continue;
                    blockOld.Statements.Add(s);
                }

                // TODO: error if new and old are not synthetic at the same time
            }

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
