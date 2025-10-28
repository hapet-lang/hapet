using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
using System;
using System.Collections.Generic;
using System.Runtime;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void PostPrepareStatementUpToCurrentStep(bool skipFirstStep, params AstStatement[] stmts)
        {
            if (stmts.Length == 0)
                return;

            if (_currentPreparationStep == PreparationStep.None)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, stmts[0], [], ErrorCode.Get(CTEN.StmtInvalidPreparation));
                return;
            }

            // to cache source file
            ProgramFile cachedSourceFile;

            // go all over the steps down
            if (_currentPreparationStep >= PreparationStep.Types && !skipFirstStep)
            {
                foreach (var stmt in stmts)
                {
                    First(stmt);
                    PostPrepareMetadataTypes(stmt, false);
                    Last(stmt);
                }
            }
            if (_currentPreparationStep >= PreparationStep.Generics)
            {
                foreach (var stmt in stmts)
                {
                    First(stmt);
                    PostPrepareMetadataGenerics(stmt);
                    bool isDecl = stmt is AstDeclaration;
                    if (isDecl) _currentParentStack.RemoveParent(); // required to update generics
                    if (isDecl) _currentParentStack.AddParent(stmt);
                    Last(stmt);
                }
                    
            }
            if (_currentPreparationStep >= PreparationStep.Inheritance)
            {
                foreach (var stmt in stmts)
                {
                    First(stmt);
                    PostPrepareMetadataInheritance(stmt);
                    Last(stmt);
                }
            }
            if (_currentPreparationStep >= PreparationStep.Delegates)
            {
                foreach (var stmt in stmts)
                {
                    First(stmt);
                    PostPrepareMetadataDelegates(stmt);
                    Last(stmt);
                }
            }
            if (_currentPreparationStep >= PreparationStep.NestedTypes)
            {
                foreach (var stmt in stmts)
                {
                    First(stmt);
                    PostPrepareMetadataNestedTypes(stmt);
                    Last(stmt);
                }
            }
            if (_currentPreparationStep >= PreparationStep.Functions)
            {
                foreach (var stmt in stmts)
                {
                    First(stmt);
                    PostPrepareMetadataFunctions(stmt);
                    Last(stmt);
                }
            }
            if (_currentPreparationStep >= PreparationStep.FieldAndPropDecls)
            {
                foreach (var stmt in stmts)
                {
                    First(stmt);
                    PostPrepareMetadataTypeFieldDecls(stmt);
                    Last(stmt);
                }
            }
            if (_currentPreparationStep >= PreparationStep.NestedTypesInside)
            {
                foreach (var stmt in stmts)
                {
                    First(stmt);
                    PostPrepareMetadataNestedTypesInside(stmt);
                    Last(stmt);
                }
            }
            if (_currentPreparationStep >= PreparationStep.FieldAndPropInits)
            {
                foreach (var stmt in stmts)
                {
                    First(stmt);
                    PostPrepareMetadataTypeFieldInits(stmt);
                    Last(stmt);
                }
            }
            if (_currentPreparationStep >= PreparationStep.Attributes)
            {
                foreach (var stmt in stmts)
                {
                    First(stmt);
                    PostPrepareMetadataAttributes(stmt);
                    Last(stmt);
                }
            }

            if (_currentPreparationStep >= PreparationStep.Inferencing)
            {
                foreach (var stmt in stmts)
                {
                    First(stmt);
                    // just handlers
                    InInfo inInfo = InInfo.Default;
                    OutInfo outInfo = OutInfo.Default;

                    // we need to inference it manually
                    if (stmt is AstClassDecl cls)
                    {
                        foreach (var decl in cls.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl).ToList())
                        {
                            PostPrepareFunctionInference(decl, inInfo, ref outInfo);
                        }
                        foreach (var decl in cls.Declarations.Where(x => x is AstDelegateDecl).Select(x => x as AstDelegateDecl).ToList())
                        {
                            PostPrepareDelegateInference(decl, inInfo, ref outInfo);
                        }
                    }
                    else if (stmt is AstStructDecl str)
                    {
                        foreach (var decl in str.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl).ToList())
                        {
                            PostPrepareFunctionInference(decl, inInfo, ref outInfo);
                        }
                        foreach (var decl in str.Declarations.Where(x => x is AstDelegateDecl).Select(x => x as AstDelegateDecl).ToList())
                        {
                            PostPrepareDelegateInference(decl, inInfo, ref outInfo);
                        }
                    }
                    else if (stmt is AstGenericDecl gen)
                    {
                        foreach (var decl in gen.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl).ToList())
                        {
                            PostPrepareFunctionInference(decl, inInfo, ref outInfo);
                        }
                        foreach (var decl in gen.Declarations.Where(x => x is AstDelegateDecl).Select(x => x as AstDelegateDecl).ToList())
                        {
                            PostPrepareDelegateInference(decl, inInfo, ref outInfo);
                        }
                    }
                    else if (stmt is AstDelegateDecl delegateDecl)
                    {
                        PostPrepareDelegateInference(delegateDecl, inInfo, ref outInfo);
                        foreach (var decl in delegateDecl.Functions)
                        {
                            PostPrepareFunctionInference(decl, inInfo, ref outInfo);
                        }
                    }
                    else if (stmt is AstFuncDecl funcDecl)
                    {
                        PostPrepareFunctionInference(funcDecl, inInfo, ref outInfo);
                    }

                    Last(stmt);
                } 
            }

            void First(AstStatement stmt)
            {
                // setting up anime shite
                cachedSourceFile = _currentSourceFile;
                _currentSourceFile = stmt.SourceFile;
                bool isDecl = stmt is AstDeclaration;
                if (isDecl) _currentParentStack.AddParent(stmt);
            }

            void Last(AstStatement stmt)
            {
                _currentSourceFile = cachedSourceFile;
                bool isDecl = stmt is AstDeclaration;
                if (isDecl) _currentParentStack.RemoveParent();
            }
        }
    }
}
