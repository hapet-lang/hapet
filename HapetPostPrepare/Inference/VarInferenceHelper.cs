using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        /// <summary>
        /// The method is used to prepare correct assignment.
        /// Like 'float a = 6;' should be allowed and 'int a = 6.0;' should not be allowed
        /// Also class/interface shite should be checked here
        /// </summary>
        /// <param name="varDecl">The var decl</param>
        private void PostPrepareVariableAssign(AstVarDecl varDecl)
        {
            var newExpr = PostPrepareExpressionWithType(varDecl.Type.OutType, varDecl.Initializer);
            varDecl.Initializer = newExpr;
        }

        /// <summary>
        /// The same as <see cref="PostPrepareVariableAssign"/> but for <see cref="AstAssignStmt"/>
        /// </summary>
        /// <param name="assignStmt">The var assignment</param>
        private void PostPrepareVariableAssign(AstAssignStmt assignStmt)
        {
            var newExpr = PostPrepareExpressionWithType(assignStmt.Target.OutType, assignStmt.Value);
            assignStmt.Value = newExpr;
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
            HapetType exprType = expr.OutType;
            AstExpression outExpr = null;

            // cringe error (probably should not be here)
            // this error is for shite like:
            // int a = TestEnum;
            // where TestEnum is a enum
            // TODO: cringe check because
            // AnimeEnum a = AnimeEnum.Test1;
            // AnimeEnum b = a;
            // has to work but it won't because of this check!!!
            if (expr is AstNestedExpr nestt && nestt.RightPart.OutType is EnumType)
            {
                if (castResult == null)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, expr, [], ErrorCode.Get(CTEN.EnumCouldNotBeAssigned));
                return expr;
            }

            // change expr type if it is an enum field
            if (expr is AstNestedExpr nest && nest.LeftPart != null && nest.LeftPart.OutType is EnumType enmT)
            {
                exprType = enmT;
            }

            if (neededType == null)
            {
                if (castResult == null)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, expr, [], ErrorCode.Get(CTEN.RequiredTypeNotEvaluated));
                return expr;
            }

            // no need for any casts if it is 'var' shite
            // just return back the expr
            if (neededType is VarType)
                return expr;

            // assigning function to delegates is made different
            if (neededType is DelegateType delT)
            {
                return PostPrepareDelegateWithType(expr, delT);
            }

            var tpName = new AstIdExpr(neededType.TypeName);
            tpName.OutType = neededType;
            tpName.Scope = expr.Scope;

            var cst = new AstCastExpr(tpName, expr, expr);
            cst.OutType = neededType;
            cst.Scope = expr.Scope;
            cst.OutValue = expr.OutValue;

            // check for user defined implicit casts
            // TODO:

            switch (neededType)
            {
                // default cringe casting
                case FloatType when exprType is IntType:
                case FloatType when exprType is FloatType:
                case FloatType when exprType is CharType:
                case IntType when exprType is CharType:
                case IntType when exprType is IntType:
                case CharType when exprType is IntType:
                    {
                        // numeric types could be narrowed
                        if (castResult != null)
                            castResult.CouldBeNarrowed = true;

                        bool? isFirstSigned = neededType switch
                        {
                            FloatType => null,
                            IntType i => i.Signed,
                            CharType => false,
                            _ => null,
                        };
                        bool? isSecondSigned = exprType switch
                        {
                            FloatType => null,
                            IntType i => i.Signed,
                            CharType => false,
                            _ => null,
                        };

                        // do not allow if signes are different or something like that. idk :)
                        // allow if the var type size is bigger or equal
                        if (neededType.GetSize() >= exprType.GetSize() &&
                            (isFirstSigned != null && isSecondSigned != null) &&
                            (isFirstSigned.Value == isSecondSigned.Value))
                        {
                            outExpr = cst;
                            break;
                        }

                        // allow shite like:
                        // byte a = 5;
                        // int b = a;
                        if (neededType.GetSize() > exprType.GetSize() &&
                            (isFirstSigned != null && isSecondSigned != null) &&
                            (isFirstSigned.Value && !isSecondSigned.Value))
                        {
                            outExpr = cst;
                            break;
                        }

                        // allow to cast all int values to float
                        // int b = 3;
                        // float a = b;
                        if (neededType.GetSize() >= exprType.GetSize() &&
                            neededType is FloatType &&
                            (exprType is IntType || exprType is CharType))
                        {
                            outExpr = cst;
                            break;
                        }

                        // there is no way to implicitly cast non-compiletime values
                        if (expr.OutValue == null)
                            break;

                        // it the value is in range of the target - then it could be easily casted :)
                        if (expr.OutValue is char charData)
                        {
                            // getting a NumberData from char UTF-16 value to normally check ranging
                            var newNumData = NumberData.FromInt(((short)charData));
                            if (newNumData.IsInRangeOfType(neededType))
                            {
                                if (castResult != null)
                                    castResult.CouldBeCasted = true;
                                outExpr = cst;
                            }
                        }
                        // it the value is in range of the target - then it could be easily casted :)
                        else if (expr.OutValue is NumberData numData && numData.IsInRangeOfType(neededType))
                        {
                            if (castResult != null)
                                castResult.CouldBeCasted = true;
                            outExpr = cst;
                        }

                        break;
                    }
                // usually when 'Anime a = new Anime();'
                case PointerType ptr when 
                    ptr.TargetType is ClassType cls1 && 
                    exprType is ClassType cls2 &&
                    cls1 == cls2:
                // usually when 'object a = animeStructInstance;'
                // usually when 'IAnime a = animeStructInstance;'
                case PointerType ptr3 when 
                    ptr3.TargetType is ClassType cls3 && 
                    exprType is StructType &&
                    (cls3.Declaration.Name.Name == "System.Object" ||
                    cls3.Declaration.Name.Name == "System.ValueType" ||
                    cls3.Declaration.IsInterface):
                    {
                        outExpr = expr;
                        if (castResult != null)
                            castResult.CouldBeCasted = true;
                        break;
                    }
                // just setting null to a pointer
                case PointerType when expr is AstNullExpr:
                // casting ptrs to null ptrs (?)
                case PointerType when exprType is PointerType ptr1 && ptr1.TargetType == null:
                case PointerType ptr2 when exprType is PointerType && ptr2.TargetType == null:
                // ptr casts
                case PointerType ptr3 when exprType is PointerType ptr4 && ptr3.TargetType == ptr4.TargetType:
                case PointerType when exprType is IntPtrType:
                case IntPtrType when exprType is PointerType:
                    {
                        outExpr = cst;
                        if (castResult != null)
                            castResult.CouldBeCasted = true;
                        break;
                    }
                    // TODO: other checks also. warn: class and class should be checked properly!!!
            }

            // if there is no way to cast
            if (neededType != exprType && outExpr == null)
            {
                string typeName = HapetType.AsString(exprType);
                if (castResult == null)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, expr, [typeName, HapetType.AsString(neededType)], ErrorCode.Get(CTEN.TypeCouldNotBeImplCasted));

                outExpr = expr;
            }
            // if the types are equal - no need to cast anything, so return orig
            else if (neededType == exprType && outExpr == null)
            {
                outExpr = expr;
                if (castResult != null)
                    castResult.CouldBeCasted = true;
            }
            return outExpr;
        }

        private AstExpression PostPrepareDelegateWithType(AstExpression value, DelegateType targetType)
        {
            // if user assigns a delegate to another
            if (value.OutType is DelegateType)
            {
                return value;
            }

            // the var is used to check when static method is accessed from an object
            bool accessingFromAnObject = false;
            var delegateParams = targetType.Declaration.Parameters;
            // TODO: probably needed when allowing delegates for non-static funcs
            //var delegateParams = targetType.Declaration.Parameters.Skip(1).ToList(); // no need for the first param

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
                PostPrepareExprInference(nestFuncName.LeftPart);
            }

            /// WARN!!! almost the same as in <see cref="PostPrepareCallExprInference"/>
            string newName = string.Empty;
            // renaming func call name from 'Anime' to 'Anime(int, float)' WITH OBJECT AS FIRST PARAM
            if (nestFuncName.LeftPart == null)
            {
                // if the type/object name is not presented - the function is in the same class
                // but we need to know is it static or not
                newName = $"{_currentClass.Name.Name}::{idFuncName.Name}{delegateParams.GetParamsString()}";
                var smbl2 = idFuncName.Scope.GetSymbol(newName);
                if (smbl2 is DeclSymbol)
                {
                    // static func defined in local class
                }
                else
                {
                    // if it is a non static func defined in local class
                    newName = $"{_currentClass.Name.Name}::{idFuncName.Name}{delegateParams.GetParamsString(PointerType.GetPointerType(_currentClass.Type.OutType))}";
                    accessingFromAnObject = true;
                    // we need to create this one because code generator requires the parameter of this shite
                    nestFuncName.LeftPart = new AstNestedExpr(new AstIdExpr("this"), null, value);
                    SetScopeAndParent(nestFuncName.LeftPart, value);
                    PostPrepareExprScoping(nestFuncName.LeftPart);
                    PostPrepareExprInference(nestFuncName.LeftPart);
                }
            }
            else if (nestFuncName.LeftPart.OutType is PointerType ptrTp && ptrTp.TargetType is ClassType clsTp)
            {
                // if we are calling like 'a.Anime()' where 'a' is an object
                // we need to rename the func name call like that:
                newName = $"{clsTp.Declaration.Name.Name}::{idFuncName.Name}{delegateParams.GetParamsString(nestFuncName.LeftPart.OutType)}";
                // check if the decl exists. if not - it could be static method call from an object
                if (clsTp.Declaration.SubScope.GetSymbol(newName) == null)
                {
                    // getting the name but without object first param
                    newName = $"{clsTp.Declaration.Name.Name}::{idFuncName.Name}{delegateParams.GetParamsString()}";
                }
                accessingFromAnObject = true;
            }
            else if (nestFuncName.LeftPart.OutType is ClassType clsTpStatic)
            {
                // if we are calling like 'A.Anime()' where 'A' is a class
                // we need to rename the func name call like that:
                newName = $"{clsTpStatic.Declaration.Name.Name}::{idFuncName.Name}{delegateParams.GetParamsString()}";
                // check if the decl exists. if not - it could be non static method call from a class name
                if (clsTpStatic.Declaration.SubScope.GetSymbol(newName) == null)
                {
                    // getting the name but without object first param
                    newName = $"{clsTpStatic.Declaration.Name.Name}::{idFuncName.Name}{delegateParams.GetParamsString(PointerType.GetPointerType(clsTpStatic))}";
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

            nestFuncName.RightPart = idFuncName.GetCopy(newName);
            PostPrepareIdentifierInference(nestFuncName.RightPart as AstIdExpr);

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
