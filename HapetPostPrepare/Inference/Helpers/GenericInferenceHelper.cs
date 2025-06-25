using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Extensions;
using HapetPostPrepare.Entities;
using System;
using System.Text;
using HapetFrontend.Helpers;
using HapetPostPrepare.Other;
using HapetFrontend.Types;
using HapetFrontend.Entities;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private AstDeclaration GetRealTypeFromGeneric(AstDeclaration decl, List<AstNestedExpr> genericTypes, 
            AstIdGenericExpr realName, bool implHasGenerics)
        {
            // we need to save previous info about current shite and then reload it 
            var savedSourceFile = _currentSourceFile;
            // we need to store it. because when inferencing new class from generic
            // we have to be sure nothing is there from previous decls
            var savedParentStack = _currentParentStack;

            // set the decl source file
            _currentSourceFile = decl.SourceFile;
            _currentParentStack = ParentStackManager.Create(_compiler.MessageHandler);

            // cringe
            string origDeclPureName = decl.Name.Name;
            if (decl is AstFuncDecl funcDecl)
            {
                // we also need to add containing parent to stack
                var parent = funcDecl.ContainingParent;
                if (parent.IsNestedDecl)
                    _currentParentStack.AddParent(parent.ParentDecl);
                _currentParentStack.AddParent(parent);
            }
            else if (decl is AstClassDecl || decl is AstStructDecl || decl is AstDelegateDecl)
            {
                origDeclPureName = decl.Name.Name.GetClassNameWithoutNamespace();
            }

            AstDeclaration realDecl;
            // if the new impl has generic types - create a declare-only copy
            if (implHasGenerics)
            {
                realDecl = decl.GetOnlyDeclareCopy();
            }
            else
            {
                // else - create a full-deep-copy 
                realDecl = decl.GetDeepCopy() as AstDeclaration;
            }

            realDecl.ContainingParent = decl.ContainingParent;
            realDecl.Parent = decl.Parent;
            realDecl.IsImplOfGeneric = true;
            realDecl.OriginalGenericDecl = decl;
            realDecl.Name = realName;
            // no need to reset HasGenericTypes when using generic shite from another generic
            realDecl.HasGenericTypes = HasAnyGenericTypes(genericTypes.Select(x => x as AstExpression).ToList());

            // resetting some shite - it has to be done again
            ResetSomeDeclParams(realDecl);

            // getting pure generics from original generic decl
            var pureGenerics = GenericsHelper.GetGenericsFromName(decl.Name as AstIdGenericExpr, _compiler.MessageHandler);
            // replaces all T with normal types like int
            MakeGenericMapping(pureGenerics, genericTypes);
            ReplaceAllGenericTypesInDecl(realDecl);
            // replaces all System.Anime::Func(Pivo) with just Func and etc.
            GenericsHelper.ResetDeclarationNames(realDecl);
            // just a pp
            PostPrepareDeclScoping(realDecl);
            // pp up to the current metadata step
            PostPrepareStatementUpToCurrentStep(realDecl);

            // no need to remove anything from the current parent stack - it would be cleared

            // reload previously saved shite
            _currentSourceFile = savedSourceFile;
            _currentParentStack = savedParentStack;

            return realDecl;
        }

        private void ResetSomeDeclParams(AstDeclaration decl)
        {
            if (decl is AstClassDecl clsDecl)
            {
                clsDecl.AllVirtualMethods = null;
            }
            else if (decl is AstStructDecl strDecl)
            {
                strDecl.AllVirtualMethods = null;
            }
        }

        /// <summary>
        /// Super cool search for generic type existance
        /// </summary>
        /// <param name="genericReals"></param>
        /// <returns></returns>
        private bool HasAnyGenericTypes(List<AstExpression> genericReals)
        {
            foreach (var g in genericReals)
            {
                if (g.OutType is GenericType)
                    return true;

                if (g is AstNestedExpr nst && nst.RightPart is AstIdGenericExpr genId)
                {
                    if (HasAnyGenericTypes(genId.GenericRealTypes))
                        return true;
                }
                
                if (g is AstIdGenericExpr genId2)
                {
                    if (HasAnyGenericTypes(genId2.GenericRealTypes))
                        return true;
                }
            }
            return false;
        }
    }
}
