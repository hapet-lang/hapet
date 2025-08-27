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
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using System.Diagnostics;

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
            realDecl.HasGenericTypes = GenericsHelper.HasAnyGenericTypes(genericTypes.Select(x => x as AstExpression).ToList());

            // getting pure generics from original generic decl
            var pureGenerics = GenericsHelper.GetGenericsFromName(decl.Name as AstIdGenericExpr, _compiler.MessageHandler);
            // replaces all T with normal types like int
            MakeGenericMapping(pureGenerics, genericTypes);
            ReplaceAllGenericTypesInDecl(realDecl);
            // replaces all System.Anime::Func(Pivo) with just Func and etc.
            GenericsHelper.ResetDeclarationNames(realDecl);

            // add invoke method to event
            if (realName.Name == "System.Event")
                AddInvokeDeclarationToEvent(realDecl as AstClassDecl, implHasGenerics);

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

        private readonly Stack<(AstDeclaration, List<AstExpression>)> _currentlyCheckingDeclarations = new Stack<(AstDeclaration, List<AstExpression>)>();
        private bool CheckIfTheTypesAreAllowedForConstrains(AstDeclaration genDecl, List<AstExpression> realTypes)
        {
            // check if the decl with types is already preparing - preventing stack overflow
            if (_currentlyCheckingDeclarations.Any(x => x.Item1 == genDecl && x.Item2 == realTypes))
                return false;

            _currentlyCheckingDeclarations.Push((genDecl, realTypes));

            var allNorm = new List<bool>();
            var pureGenerics = GenericsHelper.GetGenericsFromName(genDecl.Name as AstIdGenericExpr, _compiler.MessageHandler);
            for (int i = 0; i < pureGenerics.Count; ++i)
            {
                var currGeneric = pureGenerics[i];
                var currType = realTypes[i];
                var currContrains = genDecl.GenericConstrains.FirstOrDefault(x => x.Key.Name == currGeneric.Name).Value;

                var result = CheckIfAllowedForConstrains(currContrains, currType);
                allNorm.Add(result);
            }

            _currentlyCheckingDeclarations.Pop();

            return allNorm.All(x => x);
        }

        private bool CheckIfAllowedForConstrains(List<AstConstrainStmt> constrains, AstExpression type)
        {
            var allNorm = new List<bool>();
            foreach (var c in constrains)
            {
                bool allow = false;
                string constrainErrorName = "none";
                switch (c.ConstrainType)
                {
                    case GenericConstrainType.CustomType:
                        {
                            allow = type.OutType == c.Expr.OutType;
                            // if not the same - try to check inherited
                            if (!allow)
                            {
                                AstDeclaration decl;
                                if (type.OutType is ClassType cls)
                                    decl = cls.Declaration;
                                else if (type.OutType is GenericType gen)
                                    decl = gen.Declaration;
                                else
                                    decl = (type.OutType as StructType).Declaration;
                                allow = IsInheritedFromWithInference(decl, c.Expr.OutType);
                            }
                            constrainErrorName = HapetType.AsString(c.Expr.OutType);
                            break;
                        }
                    case GenericConstrainType.NewType:
                        {
                            InInfo inInfo = InInfo.Default;
                            OutInfo outInfo = OutInfo.Default;

                            constrainErrorName = "new"; // better text with param types?

                            if ((type.OutType is not ClassType && type.OutType is not StructType && type.OutType is not GenericType) ||
                                (type.OutType is ClassType clsT && clsT.Declaration.IsInterface))
                            {
                                allow = false;
                                break;
                            }
                            AstDeclaration decl;
                            if (type.OutType is ClassType cls)
                                decl = cls.Declaration;
                            else if (type.OutType is GenericType gen)
                                decl = gen.Declaration;
                            else
                                decl = (type.OutType as StructType).Declaration;

                            // getting all ctors of the class/struct
                            foreach (var ctor in decl.GetDeclarations().Where(x => x is AstFuncDecl f && f.ClassFunctionType == ClassFunctionType.Ctor))
                            {
                                var func = ctor as AstFuncDecl;
                                // skip first because it is a ptr to a type
                                if (func.Parameters.Count - 1 != c.AdditionalExprs.Count)
                                    continue;

                                bool allTheSame = true;
                                // go all over params and check types
                                for (int i = 0; i < func.Parameters.Count - 1; ++i)
                                {
                                    var p = func.Parameters[i + 1];
                                    var newP = c.AdditionalExprs[i];

                                    // it could be not yet infered - so infer it
                                    if (p.Type.OutType == null)
                                        PostPrepareExprInference(p.Type, inInfo, ref outInfo);

                                    if (p.Type.OutType != newP.OutType)
                                    {
                                        allTheSame = false;
                                        continue;
                                    }
                                }

                                // if all the parameter types are the same
                                if (allTheSame)
                                {
                                    allow = true;
                                    break;
                                }
                            }
                            break;
                        }
                    case GenericConstrainType.ClassType:
                        {
                            allow = type.OutType is ClassType;
                            constrainErrorName = "class";
                            break;
                        }
                    case GenericConstrainType.StructType:
                        {
                            allow = type.OutType is StructType;
                            constrainErrorName = "struct";
                            break;
                        }
                    case GenericConstrainType.DelegateType:
                        {
                            allow = type.OutType is DelegateType;
                            constrainErrorName = "delegate";
                            break;
                        }
                    case GenericConstrainType.EnumType:
                        {
                            allow = type.OutType is EnumType;
                            constrainErrorName = "enum";
                            break;
                        }
                }

                if (!allow)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, type,
                        [constrainErrorName], ErrorCode.Get(CTEN.NotSatisfyConstrain));
                }
                allNorm.Add(allow);
            }

            return allNorm.All(x => x);
        }

        private bool IsInheritedFromWithInference(AstDeclaration type, HapetType from)
        {
            if (type == null)
                return false;

            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            List<AstNestedExpr> inhFrom = type.GetInheritedTypes();
            foreach (var expr in inhFrom)
            {
                // infer if not yet
                if (expr.OutType == null)
                    PostPrepareExprInference(expr, inInfo, ref outInfo);

                var outT = expr.OutType as ClassType;
                if (outT == from || IsInheritedFromWithInference(outT.Declaration, from))
                    return true;
            }
            return false;
        }
    }
}
