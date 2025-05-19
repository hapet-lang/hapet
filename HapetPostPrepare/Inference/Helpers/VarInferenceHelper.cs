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
        public AstExpression PostPrepareExpressionWithType(AstExpression neededTypeExpr, AstExpression expr, CastResult castResult = null)
        {
            HapetType neededType = neededTypeExpr.OutType;

            // assigning function to delegates is made different
            if (neededType is DelegateType delT)
            {
                // not always could be casted - do it inside the func
                if (castResult != null)
                    castResult.CouldBeCasted = true;
                return PostPrepareDelegateWithType(expr, delT);
            }
            return _compiler.TryCastExpr(neededTypeExpr, expr, castResult, _currentSourceFile);
        }

        private AstExpression PostPrepareDelegateWithType(AstExpression value, DelegateType targetType)
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            // if user assigns a delegate to another
            if (value.OutType is DelegateType)
            {
                return value;
            }

            // the var is used to check when static method is accessed from an object
            bool accessingFromAnObject = false;
            var delegateParams = targetType.TargetDeclaration.Parameters;
            // TODO: probably needed when allowing delegates for non-static funcs
            //var delegateParams = targetType.TargetDeclaration.Parameters.Skip(1).ToList(); // no need for the first param

            // when assigning to a delegate type - function name is expected
            if (value is not AstNestedExpr nestFuncName)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, value, [], ErrorCode.Get(CTEN.DelegFuncNameExpected));
                return value;
            }
            // when assigning to a delegate type - function name is expected
            if (nestFuncName.RightPart is not AstIdExpr idFuncName)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, value, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                return value;
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
                var currentParent = _currentParentStack.GetNearestParentClassOrStruct();
                // if the type/object name is not presented - the function is in the same class
                // but we need to know is it static or not
                newName = idFuncName.GetCopy($"{currentParent.Name.Name}::{idFuncName.Name}{delegateParams.GetParamsString()}");
                var smbl2 = idFuncName.Scope.GetSymbol(newName);
                if (smbl2 is DeclSymbol)
                {
                    // static func defined in local class
                }
                else
                {
                    // if it is a non static func defined in local class
                    newName = idFuncName.GetCopy($"{currentParent.Name.Name}::{idFuncName.Name}{delegateParams.GetParamsString(PointerType.GetPointerType(currentParent.Type.OutType))}");
                    accessingFromAnObject = true;
                    // we need to create this one because code generator requires the parameter of this shite
                    nestFuncName.LeftPart = new AstNestedExpr(new AstIdExpr("this"), null, value);
                    SetScopeAndParent(nestFuncName.LeftPart, value);
                    PostPrepareExprScoping(nestFuncName.LeftPart);
                    PostPrepareExprInference(nestFuncName.LeftPart, inInfo, ref outInfo);
                }
            }
            else if (nestFuncName.LeftPart.OutType is PointerType ptrTp && ptrTp.TargetType is ClassType clsTp)
            {
                // if we are calling like 'a.Anime()' where 'a' is an object
                // we need to rename the func name call like that:
                newName = idFuncName.GetCopy($"{clsTp.Declaration.Name.Name}::{idFuncName.Name}{delegateParams.GetParamsString(nestFuncName.LeftPart.OutType)}");
                // check if the decl exists. if not - it could be static method call from an object
                if (clsTp.Declaration.SubScope.GetSymbol(newName) == null)
                {
                    // getting the name but without object first param
                    newName = idFuncName.GetCopy($"{clsTp.Declaration.Name.Name}::{idFuncName.Name}{delegateParams.GetParamsString()}");
                }
                accessingFromAnObject = true;
            }
            else if (nestFuncName.LeftPart.OutType is ClassType clsTpStatic)
            {
                // if we are calling like 'A.Anime()' where 'A' is a class
                // we need to rename the func name call like that:
                newName = idFuncName.GetCopy($"{clsTpStatic.Declaration.Name.Name}::{idFuncName.Name}{delegateParams.GetParamsString()}");
                // check if the decl exists. if not - it could be non static method call from a class name
                if (clsTpStatic.Declaration.SubScope.GetSymbol(newName) == null)
                {
                    // getting the name but without object first param
                    newName = idFuncName.GetCopy($"{clsTpStatic.Declaration.Name.Name}::{idFuncName.Name}{delegateParams.GetParamsString(PointerType.GetPointerType(clsTpStatic))}");
                    // error because user tries to access non static method from a class name
                    if (clsTpStatic.Declaration.SubScope.GetSymbol(newName) != null)
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idFuncName, [], ErrorCode.Get(CTEN.NonStaticFuncFromStatic));
                    }
                }
            }
            else
            {
                // error here: the function call could not be infered
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, value, [], ErrorCode.Get(CTEN.FuncNotInfered));
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
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, value, [], ErrorCode.Get(CTWN.StaticFuncFromObject), null, ReportType.Warning);
                }
            }
            else
            {
                // error here
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, value, [], ErrorCode.Get(CTEN.ExprExpectedToBeFunc));
            }

            return value;
        }
    }
}
