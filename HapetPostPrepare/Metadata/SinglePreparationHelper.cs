using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void PostPrepareStatementUpToCurrentStep(AstStatement stmt, bool skipFirstStep = false)
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
            if (_currentPreparationStep >= PreparationStep.Types && stmt.CurrentPreparationStep < PreparationStep.Types && !skipFirstStep)
            {
                stmt.CurrentPreparationStep = PreparationStep.Types;
                PostPrepareMetadataTypes(stmt, false);
            }
            if (_currentPreparationStep >= PreparationStep.Generics && stmt.CurrentPreparationStep < PreparationStep.Generics)
            {
                stmt.CurrentPreparationStep = PreparationStep.Generics;
                PostPrepareMetadataGenerics(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.Delegates && stmt.CurrentPreparationStep < PreparationStep.Delegates)
            {
                stmt.CurrentPreparationStep = PreparationStep.Delegates;
                PostPrepareMetadataDelegates(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.NestedTypes && stmt.CurrentPreparationStep < PreparationStep.NestedTypes) 
            {
                stmt.CurrentPreparationStep = PreparationStep.NestedTypes;
                PostPrepareMetadataNestedTypes(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.Functions && stmt.CurrentPreparationStep < PreparationStep.Functions) 
            {
                stmt.CurrentPreparationStep = PreparationStep.Functions;
                PostPrepareMetadataFunctions(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.FieldAndPropDecls && stmt.CurrentPreparationStep < PreparationStep.FieldAndPropDecls) 
            {
                stmt.CurrentPreparationStep = PreparationStep.FieldAndPropDecls;
                PostPrepareMetadataTypeFieldDecls(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.Inheritance && stmt.CurrentPreparationStep < PreparationStep.Inheritance) 
            {
                stmt.CurrentPreparationStep = PreparationStep.Inheritance;
                PostPrepareMetadataInheritance(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.InheritedFunctions && stmt.CurrentPreparationStep < PreparationStep.InheritedFunctions)
            {
                stmt.CurrentPreparationStep = PreparationStep.InheritedFunctions;
                PostPrepareMetadataInheritedFunctions(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.InheritedFieldDecls && stmt.CurrentPreparationStep < PreparationStep.InheritedFieldDecls)
            {
                stmt.CurrentPreparationStep = PreparationStep.InheritedFieldDecls;
                PostPrepareMetadataTypeInheritedFieldDecls(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.InheritedPropDecls && stmt.CurrentPreparationStep < PreparationStep.InheritedPropDecls)
            {
                stmt.CurrentPreparationStep = PreparationStep.InheritedPropDecls;
                PostPrepareMetadataTypeInheritedPropsDecls(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.NestedTypesInside && stmt.CurrentPreparationStep < PreparationStep.NestedTypesInside)
            {
                stmt.CurrentPreparationStep = PreparationStep.NestedTypesInside;
                PostPrepareMetadataNestedTypesInside(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.FieldAndPropInits && stmt.CurrentPreparationStep < PreparationStep.FieldAndPropInits)
            {
                stmt.CurrentPreparationStep = PreparationStep.FieldAndPropInits;
                PostPrepareMetadataTypeFieldInits(stmt);
            }
            if (_currentPreparationStep >= PreparationStep.Attributes && stmt.CurrentPreparationStep < PreparationStep.Attributes)
            {
                stmt.CurrentPreparationStep = PreparationStep.Attributes;
                PostPrepareMetadataAttributes(stmt);
            }

            if (_currentPreparationStep >= PreparationStep.Inferencing && stmt.CurrentPreparationStep < PreparationStep.Inferencing)
            {
                // just handlers
                InInfo inInfo = InInfo.Default;
                OutInfo outInfo = OutInfo.Default;

                stmt.CurrentPreparationStep = PreparationStep.Inferencing;

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
