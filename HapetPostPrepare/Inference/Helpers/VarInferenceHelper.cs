using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        /// <summary>
        /// WARN!!! use only for non-generic types!!!
        /// </summary>
        /// <param name="hpt"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        private AstExpression GetPreparedAst(HapetType hpt, AstStatement parent)
        {
            var ast = hpt.GetAst();
            SetScopeAndParent(ast, parent);
            PostPrepareExprScoping(ast);

            var tmp = OutInfo.Default;
            PostPrepareExprInference(ast, InInfo.Default, ref tmp);
            return ast;
        }

        /// <summary>
        /// The method tries to cast the <paramref name="expr"/> to <paramref name="neededType"/> type implicitly
        /// If it cannot the converted an error would appear
        /// </summary>
        /// <param name="neededType">The type that should be outed</param>
        /// <param name="expr">The expr to be casted</param>
        /// <returns>Casted expr</returns>
        public AstExpression PostPrepareExpressionWithType(HapetType neededType, AstExpression expr, CastResult castResult = null)
        {
            // assigning function to delegates is made different
            if (neededType is DelegateType delT)
            {
                // not always could be casted - do it inside the func
                if (castResult != null)
                    castResult.CouldBeCasted = true;
                return PostPrepareDelegateWithType(expr, delT);
            }
            return _compiler.TryCastExpr(neededType, expr, castResult, _currentSourceFile);
        }

        private AstExpression PostPrepareDelegateWithType(AstExpression value, DelegateType targetType)
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            // if user assigns a delegate to another
            if (value.OutType is DelegateType)
                return value;

            // allow nulls 
            if (value.OutType is NullType)
                return value;

            // the var is used to check when static method is accessed from an object
            bool accessingFromAnObject = false;
            var delegateParams = targetType.TargetDeclaration.Parameters;
            // TODO: probably needed when allowing delegates for non-static funcs
            //var delegateParams = targetType.TargetDeclaration.Parameters.Skip(1).ToList(); // no need for the first param

            AstExpression valueHandler = value;
            // if it is an AstIdExpr - make a nested
            if (value is AstIdExpr valueId)
            {
                valueHandler = new AstNestedExpr(valueId, null);
                valueHandler.SetDataFromStmt(valueId);
            }

            // when assigning to a delegate type - function name is expected
            if (valueHandler is not AstNestedExpr nestFuncName)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, valueHandler, [], ErrorCode.Get(CTEN.DelegFuncNameExpected));
                return valueHandler;
            }
            // when assigning to a delegate type - function name is expected
            if (nestFuncName.RightPart is not AstIdExpr idFuncName)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, valueHandler, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                return valueHandler;
            }

            // usually when in the same class
            if (nestFuncName.LeftPart != null)
            {
                // resolve the object on which func is gotten
                PostPrepareExprInference(nestFuncName.LeftPart, inInfo, ref outInfo);
            }

            /// WARN!!! almost the same as in <see cref="PostPrepareCallExprInference"/>
            AstIdExpr newName = null;
            if (idFuncName.FindSymbol != null)
            {
                // check if it was already infered somewhere
                newName = idFuncName.GetCopy();
            }
            // renaming func call name from 'Anime' to 'Anime(int, float)' WITH OBJECT AS FIRST PARAM
            else if (nestFuncName.LeftPart == null)
            {
                // at first we need to search for the symbol in local scope - it could be a var with delegate type
                var smbl2 = idFuncName.Scope.GetSymbol(idFuncName, handleGenerics: true);
                if (smbl2 is DeclSymbol dclS && dclS.Decl is not AstFuncDecl)
                {
                    newName = dclS.Name.GetCopy();
                }
                else if (smbl2 is DeclSymbol dclS2 && dclS2.Decl is AstFuncDecl func && func.IsNestedDecl)
                {
                    // func defined in function
                    newName = func.Name.GetCopy();
                }
                else
                {
                    var currentParent = _currentParentStack.GetNearestParentClassOrStruct();

                    var args = delegateParams.GetArgsFromParams();
                    var smbl3 = GetFuncFromCandidates(idFuncName, args, currentParent, false, out var _);
                    if (smbl3 != null)
                    {
                        newName = smbl3.Name.GetCopy();
                    }
                    else
                    {
                        args = delegateParams.GetArgsFromParams(currentParent.Type.OutType);
                        smbl3 = GetFuncFromCandidates(idFuncName, args, currentParent, true, out var _);
                        if (smbl3 != null)
                        {
                            // if it is a non static func defined in local class
                            newName = smbl3.Name.GetCopy();
                            accessingFromAnObject = true;

                            // we need to create this one because code generator requires the parameter of this shite
                            nestFuncName.LeftPart = new AstNestedExpr(new AstIdExpr("this"), null, valueHandler);
                            SetScopeAndParent(nestFuncName.LeftPart, valueHandler);
                            PostPrepareExprScoping(nestFuncName.LeftPart);
                            PostPrepareExprInference(nestFuncName.LeftPart, inInfo, ref outInfo);
                        }
                        else
                        {
                            // not found anything
                        }
                    }
                }
            }
            else if (nestFuncName.LeftPart.OutType is ClassType clsTp)
            {
                // this check is done to handle static-call
                if (nestFuncName.LeftPart.TryGetDeclSymbol(true) is DeclSymbol dds2 && dds2.Decl is AstClassDecl cls)
                {
                    // if we are calling like 'A.Anime()' where 'A' is a class
                    var args = delegateParams.GetArgsFromParams();
                    var smbl3 = GetFuncFromCandidates(idFuncName, args, cls, false, out var _);
                    // check if the decl exists. if not - it could be non static method call from a class name
                    if (smbl3 == null)
                    {
                        // getting the name but without object first param
                        args = delegateParams.GetArgsFromParams(clsTp);
                        smbl3 = GetFuncFromCandidates(idFuncName, args, cls, true, out var _);
                        // error because user tries to access non static method from a class name
                        if (smbl3 != null)
                        {
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idFuncName, [], ErrorCode.Get(CTEN.NonStaticFuncFromStatic));
                        }
                    }
                    else
                    {
                        newName = smbl3.Name.GetCopy();
                    }
                }
                else
                {
                    // if we are calling like 'a.Anime()' where 'a' is an object
                    var args = delegateParams.GetArgsFromParams(clsTp);
                    var smbl3 = GetFuncFromCandidates(idFuncName, args, clsTp.Declaration, true, out var _);
                    // check if the decl exists. if not - it could be static method call from an object
                    if (smbl3 == null)
                    {
                        // not found
                    }
                    else
                    {
                        newName = smbl3.Name.GetCopy();
                        accessingFromAnObject = true;
                    }
                }
            }
            if (newName == null)
            {
                // error here: the function could not be infered
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, valueHandler, [], ErrorCode.Get(CTEN.FuncNotInfered));
                return value;
            }

            nestFuncName.RightPart = newName;
            PostPrepareIdentifierInference(nestFuncName.RightPart as AstIdExpr, inInfo, ref outInfo);

            // setting parameters
            if (nestFuncName.RightPart.OutType is FunctionType ft)
            {
                // checking if it is a static func
                bool isStaticFunc = ft.Declaration.SpecialKeys.Contains(TokenType.KwStatic);
                // call expr type is the same as func return type
                nestFuncName.OutType = ft;

                // no need anymore
                nestFuncName.LeftPart = null;

                // warn if accessing from an object
                if (accessingFromAnObject && isStaticFunc)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, valueHandler, [], ErrorCode.Get(CTWN.StaticFuncFromObject), null, ReportType.Warning);
                }
            }
            else
            {
                // error here
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, valueHandler, [], ErrorCode.Get(CTEN.ExprExpectedToBeFunc));
            }

            return value;
        }
    }
}
