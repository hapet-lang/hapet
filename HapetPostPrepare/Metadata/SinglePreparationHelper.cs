using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Errors;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
using System;
using System.Runtime;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void PostPrepareStatementUpToCurrentStep(AstStatement stmt)
        {
            if (_currentPreparationStep == PreparationStep.None)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, stmt, [], ErrorCode.Get(CTEN.StmtInvalidPreparation));
                return;
            }

            // setting up anime shite
            var cachedSourceFile = _currentSourceFile;
            _currentSourceFile = stmt.SourceFile;

            if (stmt is AstDeclaration dcl)
                _currentParentStack.AddParent(dcl);

            // go all over the steps down
            if (_currentPreparationStep >= PreparationStep.Types)
            {
                PostPrepareMetadataTypes(stmt, false);
            }
            if (_currentPreparationStep >= PreparationStep.Generics)
            {
                PostPrepareMetadataGenerics(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.Inheritance)
            {
                PostPrepareMetadataInheritance(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.Delegates)
            {
                PostPrepareMetadataDelegates(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.NestedTypes)
            {
                PostPrepareMetadataNestedTypes(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.Functions)
            {
                PostPrepareMetadataFunctions(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.FieldAndPropDecls)
            {
                PostPrepareMetadataTypeFieldDecls(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.FieldAndPropInits)
            {
                PostPrepareMetadataTypeFieldInits(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.Attributes)
            {
                PostPrepareMetadataAttributes(stmt);
            }

            if (_currentPreparationStep >= PreparationStep.Inferencing)
            {
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
            }

            _currentSourceFile = cachedSourceFile;
            if (stmt is AstDeclaration)
                _currentParentStack.RemoveParent();
        }
    }
}
