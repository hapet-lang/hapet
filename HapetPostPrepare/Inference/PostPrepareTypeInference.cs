using System.Diagnostics;
using System.Diagnostics.Metrics;
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
        private void PostPrepareTypeInference()
        {
            _currentPreparationStep = PreparationStep.Inferencing;

            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            var delegates = AllDelegatesMetadata.ToList();
            var funcs = AllFunctionsMetadata.ToList();

            foreach (var funcDecl in funcs)
            {
                _currentSourceFile = funcDecl.SourceFile;

                PostPrepareFunctionInference(funcDecl, inInfo, ref outInfo);
            }

            foreach (var delegateDecl in delegates)
            {
                _currentSourceFile = delegateDecl.SourceFile;

                PostPrepareDelegateInference(delegateDecl, inInfo, ref outInfo);
            }
        }

        private void PostPrepareDelegateInference(AstDelegateDecl delegateDecl, InInfo inInfo, ref OutInfo outInfo)
        {
            /// WARN: should be already inferred in <see cref="PostPrepareMetadataTypes"/> and <see cref="PostPrepareMetadataTypeFields"/>
            /// WARN: attributes are inferrenced in <see cref="PostPrepareMetadataAttributes"/>

            var parent = delegateDecl.ContainingParent;
            if (parent != null && parent.IsNestedDecl)
                _currentParentStack.AddParent(parent.ParentDecl);
            if (parent != null)
                _currentParentStack.AddParent(parent);

            _currentParentStack.AddParent(delegateDecl);

            // inferencing parameters 
            foreach (var p in delegateDecl.Parameters)
            {
                PostPrepareParamInference(p, inInfo, ref outInfo);
            }

            // inferencing return type 
            {
                PostPrepareExprInference(delegateDecl.Returns, inInfo, ref outInfo);
            }

            _currentParentStack.RemoveParent();

            if (parent != null && parent.IsNestedDecl)
                _currentParentStack.RemoveParent();
            if (parent != null)
                _currentParentStack.RemoveParent();
        }

        public void PostPrepareFunctionInference(AstFuncDecl funcDecl, InInfo inInfo, ref OutInfo outInfo)
        {
            /// WARN: attributes are inferrenced in <see cref="PostPrepareMetadataAttributes"/>

            var parent = funcDecl.ContainingParent;
            if (parent?.IsNestedDecl ?? false)
                _currentParentStack.AddParent(parent.ParentDecl);
            if (parent != null)
                _currentParentStack.AddParent(parent);

            _currentParentStack.AddParent(funcDecl);

            // if the function inference is for metadata - infer everything except body
            // if not - infer only body because func decl already infered from metadata :)
            if (inInfo.ForMetadata)
            {
                // inferencing parameters 
                // mute all inference errors for param types of property func. 
                // if has to be errored somewhere else
                var savedMute = inInfo.MuteErrors;
                if (funcDecl.IsPropertyFunction)
                    inInfo.MuteErrors = true;
                foreach (var p in funcDecl.Parameters)
                {
                    PostPrepareParamInference(p, inInfo, ref outInfo);
                }
                if (funcDecl.IsPropertyFunction)
                    inInfo.MuteErrors = savedMute;

                // inferencing additional data
                if (funcDecl.Name.AdditionalData != null)
                    PostPrepareExprInference(funcDecl.Name.AdditionalData, inInfo, ref outInfo);

                // inferencing return type 
                {
                    // mute all inference errors for return type of property get_ func. 
                    // if has to be errored somewhere else
                    savedMute = inInfo.MuteErrors;
                    if (funcDecl.IsPropertyFunction)
                        inInfo.MuteErrors = true;
                    PostPrepareExprInference(funcDecl.Returns, inInfo, ref outInfo);
                    if (funcDecl.IsPropertyFunction)
                        inInfo.MuteErrors = savedMute;
                }

                // if the containing class is empty - it is external func
                if (funcDecl.ContainingParent != null)
                {
                    // the checks are done because it could be a nested func decl
                    Scope scopeToDefine = null;
                    AstIdExpr newName = funcDecl.Name.GetCopy();
                    if (funcDecl.ContainingParent is AstClassDecl || funcDecl.ContainingParent is AstStructDecl 
                        || funcDecl.ContainingParent is AstGenericDecl || funcDecl.ContainingParent is AstDelegateDecl)
                    {
                        scopeToDefine = funcDecl.ContainingParent.SubScope;
                    }
                    else if (funcDecl.ContainingParent is AstFuncDecl fncDeclParent)
                    {
                        scopeToDefine = fncDeclParent.Body.SubScope;
                    }
                    else
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, funcDecl.Name, [], ErrorCode.Get(CTEN.StmtNotAllowedInThis));
                        OnExit();
                        return;
                    }

                    // if it is public func - it should be visible in the scope in which func's class is
                    funcDecl.Name = newName;
                    scopeToDefine.DefineDeclSymbol(newName, funcDecl);

                    // register operator decl
                    if (funcDecl is AstOverloadDecl overDecl2)
                    {
                        if (overDecl2.OverloadType == OverloadType.UnaryOperator)
                        {
                            var op = new UserDefinedUnaryOperator(overDecl2.Operator, overDecl2.Returns.OutType, overDecl2.Parameters[0].Type.OutType);
                            op.Function = funcDecl.Type.OutType as FunctionType;
                            scopeToDefine.DefineUnaryOperator(op);
                        }
                        else if (overDecl2.OverloadType == OverloadType.BinaryOperator)
                        {
                            var op = new UserDefinedBinaryOperator(overDecl2.Operator, overDecl2.Returns.OutType, overDecl2.Parameters[0].Type.OutType, overDecl2.Parameters[1].Type.OutType);
                            op.Function = funcDecl.Type.OutType as FunctionType;
                            scopeToDefine.DefineBinaryOperator(op);
                        }
                        else if (overDecl2.OverloadType == OverloadType.ImplicitCast ||
                            overDecl2.OverloadType == OverloadType.ExplicitCast)
                        {
                            var op = new UserDefinedBinaryOperator(overDecl2.Operator, overDecl2.Returns.OutType, overDecl2.Returns.OutType, overDecl2.Parameters[0].Type.OutType);
                            op.Function = funcDecl.Type.OutType as FunctionType;
                            scopeToDefine.DefineBinaryOperator(op);
                        }
                    }
                }
                else
                {
                    // we are here only when special system functions are inferencing
                    /// like <see cref="CreateStorsCallerFunc"/>

                    funcDecl.Scope.DefineDeclSymbol(funcDecl.Name, funcDecl);
                }
            }
            else
            {
                // set nested/lambda inference
                var prev = inInfo.NestedLambdaFunctionInference;
                if (funcDecl.IsNestedDecl)
                    inInfo.NestedLambdaFunctionInference = funcDecl;

                // inferring body
                if (funcDecl.Body != null)
                    PostPrepareBlockInference(funcDecl.Body, inInfo, ref outInfo);

                // check for enough returns
                CheckThatThereIsEnoughReturnsInFunc(funcDecl);

                if (funcDecl.IsNestedDecl)
                    inInfo.NestedLambdaFunctionInference = prev;

                // check if the class if inherited from smth
                // and call base ctor
                if (funcDecl.ClassFunctionType == HapetFrontend.Enums.ClassFunctionType.Ctor &&
                    funcDecl.ContainingParent is AstClassDecl clsDecl &&
                    clsDecl.InheritedFrom.Count > 0 &&
                    funcDecl.BaseCtorCall != null &&
                    clsDecl.InheritedFrom[0].OutType is ClassType baseType &&
                    !baseType.Declaration.IsInterface &&
                    funcDecl.Body != null)
                {
                    PostPrepareExprInference(funcDecl.BaseCtorCall, inInfo, ref outInfo);

                    // preparing shite for easier code gen
                    funcDecl.BaseCtorCall.BaseType = baseType;
                    var thisArg = new AstIdExpr("this", funcDecl.BaseCtorCall) 
                    { 
                        Location = funcDecl.BaseCtorCall.Location,
                        Scope = funcDecl.Body.SubScope,
                        IsSyntheticStatement = true,
                    };
                    SetScopeAndParent(thisArg, funcDecl.Body, funcDecl.Body.SubScope);
                    PostPrepareExprInference(thisArg, inInfo, ref outInfo);
                    funcDecl.BaseCtorCall.ThisArgument = thisArg;

                    // this is a kostyl to remove previous base ctor call
                    // it is possible for impl of generic types/funcs
                    // so the base ctor call is here from previous type
                    // so we just need to remove it
                    if (funcDecl.Body.Statements.Count > 1 && funcDecl.Body.Statements[1] is AstBaseCtorStmt)
                        funcDecl.Body.Statements.RemoveAt(1);

                    // we need to insert it into block so it would be generated normally
                    // but why to the index 1? - https://stackoverflow.com/questions/140490/base-constructor-in-c-sharp-which-gets-called-first
                    funcDecl.Body.Statements.Insert(1, funcDecl.BaseCtorCall);
                }

                // check if need to call another ctor
                if (funcDecl.ClassFunctionType == HapetFrontend.Enums.ClassFunctionType.Ctor &&
                    funcDecl.ThisCtorCall != null &&
                    funcDecl.Body != null)
                {
                    PostPrepareExprInference(funcDecl.ThisCtorCall, inInfo, ref outInfo);

                    // preparing shite for easier code gen
                    funcDecl.ThisCtorCall.BaseType = null;
                    var thisArg = new AstIdExpr("this", funcDecl.ThisCtorCall)
                    {
                        Location = funcDecl.ThisCtorCall.Location,
                        Scope = funcDecl.Body.SubScope,
                        IsSyntheticStatement = true,
                    };
                    SetScopeAndParent(thisArg, funcDecl.Body, funcDecl.Body.SubScope);
                    PostPrepareExprInference(thisArg, inInfo, ref outInfo);
                    funcDecl.ThisCtorCall.ThisArgument = thisArg;

                    // this is a kostyl to remove previous this ctor call
                    // it is possible for impl of generic types/funcs
                    // so the base ctor call is here from previous type
                    // so we just need to remove it
                    var thisCCOld = funcDecl.Body.Statements.FirstOrDefault(x => x is AstBaseCtorStmt bcc && bcc.IsThisCtorCall);
                    if (thisCCOld != null) funcDecl.Body.Statements.Remove(thisCCOld);

                    // we need to insert it into block so it would be generated normally
                    // we need to insert it right after base ctor call (if exists)
                    if (funcDecl.Body.Statements.Count > 1 && funcDecl.Body.Statements[1] is AstBaseCtorStmt)
                        funcDecl.Body.Statements.Insert(2, funcDecl.ThisCtorCall);
                    else
                        funcDecl.Body.Statements.Insert(1, funcDecl.ThisCtorCall);
                }
            }

            OnExit();

            void OnExit()
            {
                _currentParentStack.RemoveParent();

                if (parent?.IsNestedDecl ?? false)
                    _currentParentStack.RemoveParent();
                if (parent != null)
                    _currentParentStack.RemoveParent();
            }
        }

        private void PostPrepareVarInference(AstVarDecl varDecl, InInfo inInfo, ref OutInfo outInfo)
        {
            // return if there is no type - error in parsing
            if (varDecl.Type == null)
                return;

            // mute all inference errors for var type of property. 
            // if has to be errored somewhere else
            var savedMute = inInfo.MuteErrors;
            if (varDecl.IsPropertyField)
                inInfo.MuteErrors = true;
            PostPrepareExprInference(varDecl.Type, inInfo, ref outInfo);
            if (varDecl.IsPropertyField)
                inInfo.MuteErrors = savedMute;

            // do not infer pure 'default' expr
            // infer at least 'default(...)' expr
            if (varDecl.Initializer != null && 
                (varDecl.Initializer is not AstDefaultExpr || (varDecl.Initializer is AstDefaultExpr def && def.TypeForDefault != null)))
                PostPrepareExprInference(varDecl.Initializer, inInfo, ref outInfo);

            // change variable type to a normal one
            if (varDecl.Type.OutType is VarType)
            {
                if (varDecl.Initializer == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, varDecl, [], ErrorCode.Get(CTEN.VarVarNoIniter));
                    return;
                }
                else if (varDecl.Initializer.OutType is VoidType)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, varDecl, [], ErrorCode.Get(CTEN.VarVoidType));
                    return;
                }
                else if (varDecl.Initializer is AstDefaultExpr def2 && def2.TypeForDefault == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, varDecl, [], ErrorCode.Get(CTEN.VarDefaultType));
                    return;
                }
                else if (varDecl.Initializer.OutType is LambdaType || varDecl.Initializer.OutType is FunctionType)
                {
                    // TODO: search better with lambda/func params checking

                    // searcch for default type - System.Action
                    var nst = new AstNestedExpr(new AstIdExpr("System.Action", varDecl.Type)
                    {
                        IsSyntheticStatement = true,
                    }, null, varDecl.Type)
                    {
                        IsSyntheticStatement = true,
                    };
                    nst.SetDataFromStmt(varDecl.Type);
                    nst.RightPart.SetDataFromStmt(varDecl.Type);
                    PostPrepareExprInference(nst, inInfo, ref outInfo);

                    varDecl.Type.OutType = nst.OutType;
                    varDecl.Type.TupleNameList = varDecl.Initializer.TupleNameList;
                }
                else
                {
                    varDecl.Type.OutType = varDecl.Initializer.OutType;
                    varDecl.Type.TupleNameList = varDecl.Initializer.TupleNameList;
                }
            }

            // pp assign value
            if (varDecl.Initializer != null)
                varDecl.Initializer = PostPrepareVarValueAssign(varDecl.Initializer, varDecl.Type.OutType, inInfo, ref outInfo, false);

            // special keys could not be allowed when the var is declared in BlockExpr
            if (!inInfo.AllowSpecialKeys)
            {
                foreach (var kk in varDecl.SpecialKeys)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, new Location(varDecl.Beginning, varDecl.Name.Ending), [kk.ToString()], ErrorCode.Get(CTEN.VarTokenNotAllowed));
                }
            }

            // check for const value that it is compile time evaluated
            if ((varDecl.Initializer == null || varDecl.Initializer.OutValue == null) && varDecl.SpecialKeys.Contains(TokenType.KwConst))
            {
                // if it is an impl of generic type - no need to error about it 
                // because probably non-deep copy was created, so do not care
                if (!(varDecl.ContainingParent.HasGenericTypes && varDecl.ContainingParent.IsImplOfGeneric))
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, varDecl.Name, [], ErrorCode.Get(CTEN.ConstValueNonComptime));
            }
        }

        private void PostPrepareParamInference(AstParamDecl paramDecl, InInfo inInfo, ref OutInfo outInfo)
        {
            // no need to inference anything when it is an 'arglist'
            if (paramDecl.ParameterModificator == ParameterModificator.Arglist)
                return;

            PostPrepareExprInference(paramDecl.Type, inInfo, ref outInfo);

            if (paramDecl.DefaultValue != null)
                PostPrepareExprInference(paramDecl.DefaultValue, inInfo, ref outInfo);
        }

        public void PostPrepareExprInference(AstStatement expr, InInfo inInfo, ref OutInfo outInfo)
        {
            switch (expr)
            {
                // special case at least for 'for' loop
                // when 'for (int i = 0;...)' where 'int i' 
                // would not be handled by blockExpr
                case AstVarDecl varDecl:
                    PostPrepareVarInference(varDecl, inInfo, ref outInfo);
                    break;

                case AstBlockExpr blockExpr:
                    PostPrepareBlockInference(blockExpr, inInfo, ref outInfo);
                    break;
                case AstUnaryExpr unExpr:
                    PostPrepareUnaryExprInference(unExpr, inInfo, ref outInfo);
                    break;
                case AstBinaryExpr binExpr:
                    PostPrepareBinaryExprInference(binExpr, inInfo, ref outInfo);
                    break;
                case AstPointerExpr pointerExpr:
                    PostPreparePointerExprInference(pointerExpr, inInfo, ref outInfo);
                    break;
                case AstAddressOfExpr addrExpr:
                    PostPrepareAddressOfExprInference(addrExpr, inInfo, ref outInfo);
                    break;
                case AstNewExpr newExpr:
                    PostPrepareNewExprInference(newExpr, inInfo, ref outInfo);
                    break;
                case AstArgumentExpr argumentExpr:
                    PostPrepareArgumentExprInference(argumentExpr, inInfo, ref outInfo);
                    break;
                case AstIdExpr idExpr:
                    PostPrepareIdentifierInference(idExpr, inInfo, ref outInfo);
                    return;
                case AstCallExpr callExpr:
                    PostPrepareCallExprInference(callExpr, inInfo, ref outInfo);
                    break;
                case AstCastExpr castExpr:
                    PostPrepareCastExprInference(castExpr, inInfo, ref outInfo);
                    break;
                case AstNestedExpr nestExpr:
                    PostPrepareNestedExprInference(nestExpr, inInfo, ref outInfo);
                    break;
                case AstDefaultExpr defaultExpr:
                    PostPrepareDefaultExprInference(defaultExpr, inInfo, ref outInfo);
                    break;
                case AstDefaultGenericExpr defaultGenericExpr:
                    PostPrepareDefaultGenericExprInference(defaultGenericExpr, inInfo, ref outInfo);
                    break;
                case AstArrayExpr arrayExpr:
                    PostPrepareArrayExprInference(arrayExpr, inInfo, ref outInfo);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    PostPrepareArrayCreateExprInference(arrayCreateExpr, inInfo, ref outInfo);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    PostPrepareArrayAccessExprInference(arrayAccExpr, inInfo, ref outInfo);
                    break;
                case AstTernaryExpr ternaryExpr:
                    PostPrepareTernaryExprInference(ternaryExpr, inInfo, ref outInfo);
                    break;
                case AstCheckedExpr checkedExpr:
                    PostPrepareCheckedExprInference(checkedExpr, inInfo, ref outInfo);
                    break;
                case AstSATOfExpr satExpr:
                    PostPrepareSATExprInference(satExpr, inInfo, ref outInfo);
                    break;
                case AstEmptyStructExpr:
                    break;
                case AstEmptyExpr:
                    break;
                case AstLambdaExpr lambdaExpr:
                    PostPrepareLambdaExprInference(lambdaExpr, inInfo, ref outInfo);
                    break;
                case AstNullableExpr nullableExpr:
                    PostPrepareNullableExprInference(nullableExpr, inInfo, ref outInfo);
                    break;
                case AstStringExpr stringExpr:
                    stringExpr.OutType = HapetType.CurrentTypeContext.StringTypeInstance;
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    // _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, assignStmt, "(Compiler exception) The statement has to be handled by block expr");
                    PostPrepareAssignStmtInference(assignStmt, inInfo, ref outInfo);
                    break;
                case AstForStmt forStmt:
                    PostPrepareForStmtInference(forStmt, inInfo, ref outInfo);
                    break;
                case AstWhileStmt whileStmt:
                    PostPrepareWhileStmtInference(whileStmt, inInfo, ref outInfo);
                    break;
                case AstDoWhileStmt doWhileStmt:
                    PostPrepareDoWhileStmtInference(doWhileStmt, inInfo, ref outInfo);
                    break;
                case AstIfStmt ifStmt:
                    PostPrepareIfStmtInference(ifStmt, inInfo, ref outInfo);
                    break;
                case AstSwitchStmt switchStmt:
                    PostPrepareSwitchStmtInference(switchStmt, inInfo, ref outInfo);
                    break;
                case AstCaseStmt caseStmt:
                    PostPrepareCaseStmtInference(caseStmt, inInfo, ref outInfo);
                    break;
                case AstBreakContStmt _:
                    break;
                case AstReturnStmt returnStmt:
                    PostPrepareReturnStmtInference(returnStmt, inInfo, ref outInfo);
                    break;
                case AstAttributeStmt attrStmt:
                    PostPrepareAttributeStmtInference(attrStmt, inInfo, ref outInfo);
                    break;
                case AstBaseCtorStmt baseStmt:
                    PostPrepareBaseCtorStmtInference(baseStmt, inInfo, ref outInfo);
                    break;
                case AstThrowStmt throwStmt:
                    PostPrepareThrowStmtInference(throwStmt, inInfo, ref outInfo);
                    break;
                case AstTryCatchStmt tryCatchStmt:
                    PostPrepareTryCatchStmtInference(tryCatchStmt, inInfo, ref outInfo);
                    break;
                case AstCatchStmt catchStmt:
                    PostPrepareCatchStmtInference(catchStmt, inInfo, ref outInfo);
                    break;
                case AstGotoStmt gotoStmt:
                    PostPrepareGotoStmtInference(gotoStmt, inInfo, ref outInfo);
                    break;

                // skip literals
                case AstNumberExpr:
                //case AstStringExpr:
                case AstBoolExpr:
                case AstCharExpr:
                case AstNullExpr:
                    break;

                default:
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, expr, [expr.AAAName], ErrorCode.Get(CTEN.StmtNotImplemented));
                        break;
                    }
            }
        }

        public void PostPrepareBlockInference(AstBlockExpr blockExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            var prevBlock = _currentBlock;
            _currentBlock = blockExpr;
            // go all over the statements
            foreach (var stmt in blockExpr.Statements.ToList())
            {
                if (stmt == null)
                    continue;
                // skip nested, they are prepared by themselfzz
                if (stmt is AstFuncDecl)
                    continue;
                PostPrepareExprInference(stmt, inInfo, ref outInfo);

                // handle weak return statements
                if (outInfo.NeedToAddFromWeakReturn.Count > 0)
                {
                    int currI = blockExpr.Statements.IndexOf(stmt);
                    blockExpr.Statements.Insert(currI, outInfo.NeedToAddFromWeakReturn.Pop());
                }
            }

            _currentBlock = prevBlock;
        }

        private void PostPrepareUnaryExprInference(AstUnaryExpr unExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // TODO: check for the right size for an existance value (compiletime evaluated) and do some shite (set unExpr OutValue)
            PostPrepareExprInference(unExpr.SubExpr as AstExpression, inInfo, ref outInfo);

            // there was a error previously
            if (unExpr.SubExpr.OutType == null)
                return;

            var operators = unExpr.Scope.GetUnaryOperators(unExpr.Operator, unExpr.SubExpr.OutType);
            if (operators.Count == 0)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, unExpr, 
                    [unExpr.Operator, HapetType.AsString((unExpr.SubExpr as AstExpression).OutType)], ErrorCode.Get(CTEN.UndefOpForType));
            }
            else if (operators.Count > 1)
            {
                // TODO: tell em where are the operators defined
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, unExpr, 
                    [unExpr.Operator, HapetType.AsString((unExpr.SubExpr as AstExpression).OutType)], ErrorCode.Get(CTEN.TooManyOpsForType));
            }
            else
            {
                unExpr.ActualOperator = operators[0];
                unExpr.OutType = unExpr.ActualOperator.ResultType;

                // this is done to make proper pointer type as out
                // probably non of unary operators would
                // change the pointer somehow (deref is handled in another way)
                if (unExpr.OutType is PointerType ptrT)
                {
                    // WARN: probably should work :)
                    unExpr.OutType = unExpr.SubExpr.OutType;
                }

                // special checks for enums
                if (unExpr.SubExpr.OutType is EnumType enm1 && (unExpr.Operator == "~"))
                {
                    unExpr.OutType = enm1;
                }

                // if the value could be evaluated at the compile time
                if ((unExpr.SubExpr as AstExpression).OutValue != null && 
                    unExpr.ActualOperator.CanExecute)
                {
                    unExpr.OutValue = unExpr.ActualOperator.Execute((unExpr.SubExpr as AstExpression).OutValue);
                }
            }
        }

        private void PostPrepareBinaryExprInference(AstBinaryExpr binExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // wrong parsing
            if (binExpr.Left == null || binExpr.Right == null)
                return;

            // resolve the actual operator in the current scope
            PostPrepareExprInference(binExpr.Left as AstExpression, inInfo, ref outInfo);
            PostPrepareExprInference(binExpr.Right as AstExpression, inInfo, ref outInfo);

            // error somewhere previously
            if (binExpr.Left.OutType == null || binExpr.Right.OutType == null)
                return;

            var operators = binExpr.Scope.GetBinaryOperators(binExpr.Operator, binExpr.Left.OutType, binExpr.Right.OutType);
            if (operators.Count == 0)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, binExpr, 
                    [binExpr.Operator, 
                    HapetType.AsString((binExpr.Left as AstExpression).OutType), 
                    HapetType.AsString((binExpr.Right as AstExpression).OutType)], 
                    ErrorCode.Get(CTEN.BinUndefOpForTypes));
            }
            else if (operators.Count > 1)
            {
                // TODO: tell em where are the operators defined
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, binExpr, 
                    [binExpr.Operator,
                    HapetType.AsString((binExpr.Left as AstExpression).OutType),
                    HapetType.AsString((binExpr.Right as AstExpression).OutType)], 
                    ErrorCode.Get(CTEN.BinTooManyOpsForTypes));
            }
            else
            {
                binExpr.ActualOperator = operators[0];
                binExpr.OutType = binExpr.ActualOperator.ResultType;

                // making some type casts
                var leftExpr = (binExpr.Left as AstExpression);
                var rightExpr = (binExpr.Right as AstExpression);

                // CRINGE :) special cases for as/is/in
                switch (binExpr.ActualOperator.Name)
                {
                    case "as":
                        {
                            // make a warning if struct 'as' cast used (that is not generated by 'is' op)
                            if (rightExpr.OutType is StructType && !binExpr.IsFromIsOperator && !_currentSourceFile.IsImported)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile, rightExpr,
                                    [], ErrorCode.Get(CTWN.StructCastWithAs), null, ReportType.Warning);

                            binExpr.OutType = rightExpr.OutType;
                            // TODO: check for inheritance!!!
                            break;
                        }
                    case "is":
                        {
                            binExpr.OutType = HapetType.CurrentTypeContext.BoolTypeInstance;
                            // TODO: check for inheritance!!!
                            // TODO: many checks with valueType usage
                            break;
                        }
                    default:
                        {
                            // if smth with pointers :(((
                            if (binExpr.OutType is PointerType)
                            {
                                // we need to multiply one of the expr by size of ptr type size
                                if (leftExpr.OutType is PointerType ptrT)
                                {
                                    // error if bin op with void*
                                    if (ptrT.TargetType is VoidType)
                                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, leftExpr, [binExpr.ActualOperator.Name], ErrorCode.Get(CTEN.OpUsedWithVoidPtr));
                                    var parent = rightExpr.NormalParent;
                                    var mulK = new AstNumberExpr(NumberData.FromObject(ptrT.TargetType.GetSize()), null, null, rightExpr)
                                    {
                                        IsSyntheticStatement = true,
                                    };
                                    SetScopeAndParent(mulK, parent);
                                    rightExpr = new AstBinaryExpr("*", rightExpr, mulK, rightExpr);
                                    SetScopeAndParent(rightExpr, parent);
                                    PostPrepareExprInference(rightExpr, inInfo, ref outInfo);
                                    binExpr.Right = rightExpr;

                                    // also change the outType of binExpr
                                    binExpr.OutType = ptrT;
                                }
                                else if (rightExpr.OutType is PointerType ptrT2)
                                {
                                    // error if bin op with void*
                                    if (ptrT2.TargetType is VoidType)
                                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, rightExpr, [binExpr.ActualOperator.Name], ErrorCode.Get(CTEN.OpUsedWithVoidPtr));
                                    var parent = leftExpr.NormalParent;
                                    var mulK = new AstNumberExpr(NumberData.FromObject(ptrT2.TargetType.GetSize()), null, null, leftExpr)
                                    {
                                        IsSyntheticStatement = true,
                                    };
                                    SetScopeAndParent(mulK, parent);
                                    leftExpr = new AstBinaryExpr("*", leftExpr, mulK, leftExpr);
                                    SetScopeAndParent(leftExpr, parent);
                                    PostPrepareExprInference(leftExpr, inInfo, ref outInfo);
                                    binExpr.Left = leftExpr;

                                    // also change the outType of binExpr
                                    binExpr.OutType = ptrT2;
                                }
                            }

                            // special checks for enums
                            if (binExpr.Right.OutType is EnumType enm1 &&
                                binExpr.Left.OutType is EnumType enm2 &&
                                (binExpr.Operator == "|" || binExpr.Operator == "&"))
                            {
                                if (enm1 != enm2)
                                {
                                    // TODO: error - has to be the same
                                }
                                binExpr.OutType = enm1;
                            }

                            // make some casts inside
                            HandleBinExpr(binExpr);

                            // if the value could be evaluated at the compile time
                            if (leftExpr.OutValue != null && 
                                rightExpr.OutValue != null && 
                                binExpr.ActualOperator.CanExecute)
                            {
                                binExpr.OutValue = binExpr.ActualOperator.Execute(leftExpr.OutValue, rightExpr.OutValue);
                            }

                            break;
                        }
                }

            }
        }

        private void PostPreparePointerExprInference(AstPointerExpr pointerExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // prepare the right side
            PostPrepareExprInference(pointerExpr.SubExpression, inInfo, ref outInfo);
            if (pointerExpr.IsDereference)
            {
                // if it is a deref - right type has to be a ptr
                var rightType = pointerExpr.SubExpression.OutType as PointerType;
                if (rightType == null)
                {
                    // error here
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile,
                        pointerExpr.SubExpression, [HapetType.AsString(pointerExpr.SubExpression.OutType)],
                        ErrorCode.Get(CTEN.PointerTypeExpected));
                    return;
                }
                pointerExpr.OutType = rightType.TargetType;
            }
            else
            {
                // create a new pointer type from the right side and set the type to itself
                pointerExpr.OutType = PointerType.GetPointerType(pointerExpr.SubExpression.OutType);
            }
        }

        private void PostPrepareAddressOfExprInference(AstAddressOfExpr addrExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // prepare the right side
            PostPrepareExprInference(addrExpr.SubExpression, inInfo, ref outInfo);
            addrExpr.OutType = PointerType.GetPointerType(addrExpr.SubExpression.OutType);
        }

        private void PostPrepareNewExprInference(AstNewExpr newExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            foreach (var a in newExpr.Arguments)
            {
                PostPrepareExprInference(a, inInfo, ref outInfo);
            }

            // we need to create types here because at TupleReplace time 
            // we don't know the types of the expr
            if (newExpr.IsTupleCreation)
            {
                var genId = (newExpr.TypeName as AstNestedExpr).RightPart as AstIdGenericExpr;
                var types = newExpr.Arguments.Select(x => GetPreparedAst(x.OutType, x));
                genId.GenericRealTypes.AddRange(types);
            }

            // prepare the right side
            PostPrepareExprInference(newExpr.TypeName, inInfo, ref outInfo);
            // the type of newExpr is the same as the type of its name expr
            newExpr.OutType = newExpr.TypeName.OutType;

            // error if they trying to create an instance from interface of an abstract class
            if (newExpr.TypeName.OutType is ClassType clsType && 
                (clsType.Declaration.IsInterface || 
                clsType.Declaration.SpecialKeys.Contains(TokenType.KwAbstract)))
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, newExpr, [], ErrorCode.Get(CTEN.CreateInterfOrAbsCls));
            }

            // searching for ctor
            if (newExpr.TypeName.OutType is ClassType || newExpr.TypeName.OutType is StructType)
            {
                // getting decl
                AstDeclaration decl;
                if (newExpr.TypeName.OutType is ClassType clsT)
                    decl = clsT.Declaration;
                else
                    decl = (newExpr.TypeName.OutType as StructType).Declaration;

                string onlyName = decl.Name.Name.GetClassNameWithoutNamespace();
                var ctorName = $"{onlyName}_ctor";
                List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(newExpr.Arguments);
                argsWithClassParam.Insert(0, new AstArgumentExpr(new AstIdExpr("this")
                {
                    OutType = newExpr.OutType,
                    Scope = newExpr.Scope,
                    IsSyntheticStatement = true,
                })
                {
                    OutType = newExpr.OutType,
                    Scope = newExpr.Scope,
                    IsSyntheticStatement = true,
                });

                // add ref to first param if struct
                if (decl is AstStructDecl)
                    argsWithClassParam[0].ArgumentModificator = ParameterModificator.Ref;

                var ctorSymbol = GetFuncFromCandidates(new AstIdExpr(ctorName), argsWithClassParam, decl, true, out var casts);
                // error if ctor not found
                if (ctorSymbol == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, newExpr.TypeName, [decl.Name.Name], ErrorCode.Get(CTEN.CtorWithArgTypesNotFound));
                    return;
                }
                newExpr.ConstructorSymbol = ctorSymbol as DeclSymbol;

                // replace with casts to required
                newExpr.Arguments.ReplaceWithCasts(casts.Skip(1).ToList());
            }
        }

        private void PostPrepareArgumentExprInference(AstArgumentExpr argumentExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // could be null on wrong parsing
            if (argumentExpr.Expr != null)
                PostPrepareExprInference(argumentExpr.Expr, inInfo, ref outInfo);

            if (argumentExpr.Name != null)
            {
                // WARN: do not infer the arg name. it has to be errored while candidating
                // PostPrepareExprInference(argumentExpr.Name, inInfo, ref outInfo);
            }

            // the argument type is the same as its expr type
            argumentExpr.OutType = argumentExpr.Expr?.OutType;
            // if the value could be evaluated at the compile time
            if (argumentExpr.Expr?.OutValue != null)
            {
                argumentExpr.OutValue = argumentExpr.Expr.OutValue;
            }
        }

        private void PostPrepareCastExprInference(AstCastExpr castExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(castExpr.SubExpression, inInfo, ref outInfo);
            // it could be null :))
            if (castExpr.TypeExpr != null)
            {
                PostPrepareExprInference(castExpr.TypeExpr, inInfo, ref outInfo);
                castExpr.OutType = castExpr.TypeExpr.OutType;

                // check that the cast is possible
                var castResult = new CastResult();
                PostPrepareExpressionWithType(castExpr.TypeExpr.OutType, castExpr.SubExpression, castResult);
                if (!castResult.CouldBeCasted && !castResult.CouldBeNarrowed)
                {
                    // error - impossible cast
                    // TODO: only if really impossible like (cls1)notInheritedCls2
                    //_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, castExpr.SubExpression, 
                    //    [HapetType.AsString(castExpr.SubExpression.OutType), HapetType.AsString(castExpr.TypeExpr.OutType)],
                    //    ErrorCode.Get(CTEN.TypeCouldNotBeImplCasted));
                }
            }
            else
            {
                castExpr.OutType = castExpr.SubExpression.OutType;
            }

            castExpr.OutValue = castExpr.SubExpression.OutValue; // WARN: is it ok just to pass the value? - no. should be constcasted like from int to double TODO:
        }

        private void PostPrepareNestedExprInference(AstNestedExpr nestExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // null on wrong parsing
            if (nestExpr.RightPart == null)
                return;

            // the var is used to check when static/const field is accessed from an object
            bool accessingFromAnObject = false;

            bool foundNs = false;
            // normalizing types with their namespaces
            InternalNormalizeLeftPartIfItIsANamespaceWithType(nestExpr, ref foundNs);

            if (nestExpr.LeftPart == null)
            {
                PostPrepareExprInference(nestExpr.RightPart, inInfo, ref outInfo);
                nestExpr.OutType = nestExpr.RightPart.OutType;
                nestExpr.TupleNameList = nestExpr.RightPart.TupleNameList;
                nestExpr.OutValue = nestExpr.RightPart.OutValue;

                // kostyl to add 'this' as left part 
                if (nestExpr.RightPart is AstIdExpr idExpr && 
                    idExpr.FindSymbol is DeclSymbol dS && 
                    dS.Decl is AstVarDecl vD && 
                    (vD.ContainingParent is AstClassDecl || vD.ContainingParent is AstStructDecl) &&
                    !vD.SpecialKeys.Contains(TokenType.KwStatic) &&
                    !vD.SpecialKeys.Contains(TokenType.KwConst))
                {
                    var thisArg = new AstNestedExpr(new AstIdExpr("this", nestExpr)
                    {
                        Location = nestExpr.Location,
                        IsSyntheticStatement = true,
                    }, null, nestExpr)
                    {
                        Location = nestExpr.Location,
                        IsSyntheticStatement = true,
                    };
                    SetScopeAndParent(thisArg, nestExpr);
                    PostPrepareExprScoping(thisArg);
                    PostPrepareExprInference(thisArg, inInfo, ref outInfo);
                    nestExpr.LeftPart = thisArg;
                }
            }
            else
            {
                AstDeclaration leftSideDecl = null;
                PostPrepareExprInference(nestExpr.LeftPart, inInfo, ref outInfo);

                accessingFromAnObject = true;
                // if left part found symbol is a class decl - static access
                if (nestExpr.LeftPart.TryGetDeclSymbol() is DeclSymbol ds1 && 
                    (ds1.Decl is AstClassDecl || ds1.Decl is AstStructDecl))
                {
                    accessingFromAnObject = false;
                }
                // it is not possible for enums to be objects
                if (nestExpr.LeftPart.OutType is EnumType)
                {
                    accessingFromAnObject = false;
                }

                if (nestExpr.LeftPart.OutType is ClassType classTT)
                    leftSideDecl = classTT.Declaration;
                else if (nestExpr.LeftPart.OutType is StructType structt)
                    leftSideDecl = structt.Declaration;
                else if (nestExpr.LeftPart.OutType is EnumType enumT)
                    leftSideDecl = enumT.Declaration;
                else if (nestExpr.LeftPart.OutType is GenericType genT)
                    leftSideDecl = genT.Declaration;

                if (leftSideDecl == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, nestExpr.LeftPart, [], ErrorCode.Get(CTEN.ExprNotClassOrStruct));
                    return;
                }

                // here could only be an AstIdExpr because AstCallExpr and AstExpression would be in 'if' block upper
                if (nestExpr.RightPart is not AstIdExpr idExpr)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, nestExpr.RightPart, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                    return;
                }

                // handle here tuple custom names
                if (nestExpr.LeftPart.TryGetDeclSymbol() is DeclSymbol ds2 && 
                    (ds2.Decl is AstVarDecl || ds2.Decl is AstParamDecl) &&
                    ds2.Decl.Type.TupleNameList != null &&
                    leftSideDecl.NameWithNs == "System.ValueTuple")
                {
                    var entry = ds2.Decl.Type.TupleNameList.FirstOrDefault(x => x.Name == idExpr.Name);
                    var entryIndex = ds2.Decl.Type.TupleNameList.IndexOf(entry);
                    idExpr = idExpr.GetCopy($"Item{entryIndex + 1}");
                }

                var saved = inInfo.MuteErrors;
                inInfo.MuteErrors = true;
                if (idExpr.Name == "__syntheticVar")
                {

                }
                // searching for the symbol in the class/struct
                PostPrepareIdentifierInference(idExpr, inInfo, ref outInfo, leftSideDecl);
                inInfo.MuteErrors = saved;

                var smbl = idExpr.FindSymbol;
                if (smbl is DeclSymbol typed)
                {
                    nestExpr.RightPart = idExpr.GetCopy();
                    nestExpr.RightPart.OutType = typed.Decl.Type.OutType;
                    (nestExpr.RightPart as AstIdExpr).FindSymbol = idExpr.FindSymbol;
                    nestExpr.OutType = idExpr.OutType;
                    nestExpr.TupleNameList = idExpr.TupleNameList;
                    nestExpr.OutValue = idExpr.OutValue;

                    // check if the var is a static/const field and user is accessing it from an object
                    if (typed.Decl is AstVarDecl varDecl && (varDecl.SpecialKeys.Contains(TokenType.KwStatic) || varDecl.SpecialKeys.Contains(TokenType.KwConst)) && accessingFromAnObject) // if accessing from an object - give em a warning :)
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, idExpr, [], ErrorCode.Get(CTWN.StaticFieldFromObject), null, HapetFrontend.Entities.ReportType.Warning);
                    }
                }
                else
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, idExpr, [HapetType.AsString(nestExpr.LeftPart.OutType)], ErrorCode.Get(CTEN.SymbolNotFoundInType));
                }
            }
        }

        // :)
        /// <summary>
        /// This shite is used to join namespace with type (if exist) to a one AstIdExpr as a right part
        /// If we have AstNested like 'System.Runtime.InteropServices.DllImportAttribute.DllName'
        /// I would like to have 'System.Runtime.InteropServices.DllImportAttribute' as one AstId
        /// Because it is just a type
        /// </summary>
        /// <param name="nestExpr">The shite</param>
        private void InternalNormalizeLeftPartIfItIsANamespaceWithType(AstNestedExpr nestExpr, ref bool found)
        {
            string flatten = nestExpr.TryFlatten(null, null);
            if (string.IsNullOrWhiteSpace(flatten))
                return; // no need to normalize this shite :)

            if (nestExpr.LeftPart == null)
                return;

            InternalNormalizeLeftPartIfItIsANamespaceWithType(nestExpr.LeftPart, ref found);

            // this could be a func call or array access
            if (nestExpr.LeftPart.RightPart is not AstIdExpr idExpr)
                return;

            // check is it namespace
            string leftString = idExpr.Name;
            bool foundNs = nestExpr.Scope.IsStringNamespaceOrPart(leftString);
            // go all over the usings
            foreach (var usng in _currentSourceFile.Usings)
            {
                // getting ns string
                var ns = usng.FlattenNamespace;
                if (nestExpr.Scope.IsStringNamespaceOrPart($"{ns}.{leftString}"))
                {
                    foundNs = true;
                    break;
                }
            }

            // check is it namespace
            if (foundNs)
            {
                // if it is a namespace - join with current right side and try again
                nestExpr.RightPart = (nestExpr.RightPart as AstIdExpr).GetCopy($"{leftString}.{(nestExpr.RightPart as AstIdExpr).Name}");
                nestExpr.LeftPart = null;
            }
            else
            {
                if (!found)
                {
                    // if it is not a namespace - then probably type is done
                    nestExpr.LeftPart.LeftPart = null;
                    nestExpr.LeftPart.RightPart.Location = nestExpr.LeftPart.Location;
                    found = true;
                }
            }
        }

        private void PostPrepareDefaultExprInference(AstDefaultExpr defaultExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            if (defaultExpr.TypeForDefault == null)
            {
                // try to assign current func's return type
                var func = _currentParentStack.GetNearestParentFuncOrLambda();
                var funcRet = func is AstFuncDecl fnc ? fnc.Returns : (func as AstLambdaExpr).Returns;

                if (func != null && funcRet.OutType is not VoidType)
                    defaultExpr.TypeForDefault = funcRet.GetDeepCopy() as AstExpression;
                else
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, defaultExpr, [], ErrorCode.Get(CTEN.DefaultWasNotInfered));
                    return;
                }                
            }
            PostPrepareExprInference(defaultExpr.TypeForDefault, inInfo, ref outInfo);
            defaultExpr.OutType = defaultExpr.TypeForDefault.OutType;
        }

        private void PostPrepareDefaultGenericExprInference(AstDefaultGenericExpr defaultExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            if (defaultExpr.TypeForDefault == null)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, defaultExpr, [], ErrorCode.Get(CTEN.DefaultWasNotInfered));
                return;
            }
            defaultExpr.OutType = defaultExpr.TypeForDefault;
        }

        private void PostPrepareArrayExprInference(AstArrayExpr arrayExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(arrayExpr.SubExpression, inInfo, ref outInfo);
            arrayExpr.OutType = GetArrayType(arrayExpr.SubExpression, arrayExpr);
        }

        private void PostPrepareArrayCreateExprInference(AstArrayCreateExpr arrayExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            foreach (var sz in arrayExpr.SizeExprs)
            {
                PostPrepareExprInference(sz, inInfo, ref outInfo);
            }

            PostPrepareExprInference(arrayExpr.TypeName, inInfo, ref outInfo);

            // checks if stackalloc
            if (arrayExpr.IsStackAlloc)
            {
                // only 1d alloc is allowed
                if (arrayExpr.SizeExprs.Count != 1)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, arrayExpr.TypeName, [], ErrorCode.Get(CTEN.StackAllocOnly1DArray));
                // only structs are allowed
                if (arrayExpr.TypeName.OutType is not StructType)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, arrayExpr.TypeName, [], ErrorCode.Get(CTEN.StackAllocOnlyStructs));
            }

            // create an expecting elements type to be
            HapetType expectingElementType = arrayExpr.TypeName.OutType;
            int sizeAmount = arrayExpr.SizeExprs.Count;
            // preparing for ndim arrays
            while (sizeAmount > 1)
            {
                expectingElementType = GetArrayType(arrayExpr.TypeName, arrayExpr);
                sizeAmount--;
            }

            // infer elements
            for (int i = 0; i < arrayExpr.Elements.Count; ++i)
            {
                var e = arrayExpr.Elements[i];
                PostPrepareExprInference(e, inInfo, ref outInfo);
                // try to use implicit cast if it can be used
                arrayExpr.Elements[i] = PostPrepareExpressionWithType(expectingElementType, e);
            }

            // preparing the array
            PostPrepareFullArray(arrayExpr);
        }

        private void PostPrepareArrayAccessExprInference(AstArrayAccessExpr arrayAccExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // there were problems before
            if (arrayAccExpr.ParameterExpr == null || arrayAccExpr.ObjectName == null)
                return;

            PostPrepareExprInference(arrayAccExpr.ParameterExpr, inInfo, ref outInfo);
            PostPrepareExprInference(arrayAccExpr.ObjectName, inInfo, ref outInfo);

            // at first try to find indexer overload
            string typeName = null;
            HapetType firstParamType = null;
            AstArgumentExpr pseudoFirstArg = null;
            Scope subScope = null;
            AstDeclaration declItself = null;
            if (arrayAccExpr.ObjectName.OutType is ClassType clsT)
            {
                typeName = clsT.Declaration.Name.Name;
                firstParamType = arrayAccExpr.ObjectName.OutType;
                pseudoFirstArg = new AstArgumentExpr(arrayAccExpr.ObjectName) { Scope = arrayAccExpr.ObjectName.Scope };
                subScope = clsT.Declaration.SubScope;
                declItself = clsT.Declaration;
            }
            else if (arrayAccExpr.ObjectName.OutType is StructType strT)
            {
                typeName = strT.Declaration.Name.Name;
                firstParamType = arrayAccExpr.ObjectName.OutType;
                pseudoFirstArg = new AstArgumentExpr(arrayAccExpr.ObjectName) { Scope = arrayAccExpr.ObjectName.Scope, ArgumentModificator = ParameterModificator.Ref };
                subScope = strT.Declaration.SubScope;
                declItself = strT.Declaration;
            }
            if (typeName != null)
            {
                // TODO: this cringe should be rewritten
                // it can find only one but there could be multiple

                var saved = inInfo.MuteErrors;
                inInfo.MuteErrors = true;
                var idExpr = new AstIdExpr("indexer__");
                idExpr.SetDataFromStmt(arrayAccExpr);
                PostPrepareIdentifierInference(idExpr, inInfo, ref outInfo, declItself);
                inInfo.MuteErrors = saved;

                if (idExpr.FindSymbol is DeclSymbol ds && ds.Decl is AstIndexerDecl indDecl)
                {
                    var cstResult = new CastResult();
                    _compiler.TryCastExpr(indDecl.IndexerParameter.Type.OutType, arrayAccExpr.ParameterExpr, cstResult);
                    if (!cstResult.CouldBeCasted)
                    {
                        // TODO: error
                    }

                    arrayAccExpr.OutType = indDecl.Type.OutType;
                    arrayAccExpr.IndexerDecl = indDecl;
                    arrayAccExpr.ParameterExpr = PostPrepareVarValueAssign(arrayAccExpr.ParameterExpr, indDecl.IndexerParameter.Type.OutType, inInfo, ref outInfo);
                    return; // everything is ok :)
                }
            }

            if (arrayAccExpr.ParameterExpr.OutType is not IntType)
            {
                // error here? i cannot access array if it is not an int type
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, arrayAccExpr.ParameterExpr, [], ErrorCode.Get(CTEN.ArrayIndexNotInt));
            }

            HapetType outType = null;
            if (arrayAccExpr.ObjectName.OutType is PointerType ptrType)
                outType = ptrType.TargetType;
            else
            {
                // error because expected an array 
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, arrayAccExpr.ObjectName, [], ErrorCode.Get(CTEN.NonStringOrArrayIndexed));
            }
            arrayAccExpr.OutType = outType;
        }

        private void PostPrepareTernaryExprInference(AstTernaryExpr expr, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(expr.Condition, inInfo, ref outInfo);
            if (expr.Condition.OutType is not BoolType) 
            {
                // error
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, expr.Condition, [], ErrorCode.Get(CTEN.ExprIsNotBool));
            }

            // this handles defaults
            expr.TrueExpr = PostPrepareVarValueAssign(expr.TrueExpr, null, inInfo, ref outInfo);
            expr.FalseExpr = PostPrepareVarValueAssign(expr.FalseExpr, null, inInfo, ref outInfo);

            // this could be when 'a?.FuncTest();' is made
            if (expr.TrueExpr.OutType is VoidType || expr.FalseExpr.OutType is VoidType)
            {
                expr.OutType = expr.TrueExpr.OutType is VoidType ? expr.TrueExpr.OutType : expr.FalseExpr.OutType;
                return;
            }

            // try to cast to the false type
            var castResult1 = new CastResult();
            PostPrepareExpressionWithType(expr.TrueExpr.OutType, expr.FalseExpr, castResult1);
            // setting the basest type
            // imagine having 'cond ? IEnumerable : List'
            // then IEnumerable should be returned as the most basest type
            if (castResult1.CouldBeCasted)
            {
                expr.OutType = expr.TrueExpr.OutType; 
            }
            else
            {
                // try to cast to the true type
                var castResult2 = new CastResult();
                PostPrepareExpressionWithType(expr.FalseExpr.OutType, expr.TrueExpr, castResult2);
                if (castResult2.CouldBeCasted)
                {
                    expr.OutType = expr.FalseExpr.OutType;
                }
                else
                {
                    // special case for nullable
                    if ((expr.TrueExpr.OutType is NullType && expr.FalseExpr.OutType is StructType) ||
                        (expr.TrueExpr.OutType is StructType && expr.FalseExpr.OutType is NullType))
                    {
                        var nullableType = HapetType.CurrentTypeContext.GetNullableType(
                            expr.TrueExpr.OutType is StructType ? expr.TrueExpr.OutType : expr.FalseExpr.OutType);
                        expr.OutType = nullableType;
                    }
                    else
                    {
                        // error that the types are not connected to each other
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, expr.Location,
                            [HapetType.AsString(expr.TrueExpr.OutType), HapetType.AsString(expr.FalseExpr.OutType)],
                            ErrorCode.Get(CTEN.TypeOfTernaryNotDeterminated));
                    }
                }
            }

            // this handles casts
            expr.TrueExpr = PostPrepareVarValueAssign(expr.TrueExpr, expr.OutType, inInfo, ref outInfo);
            expr.FalseExpr = PostPrepareVarValueAssign(expr.FalseExpr, expr.OutType, inInfo, ref outInfo);

            // evaluate at comptime if possible
            if (expr.Condition.OutValue is bool &&
                expr.TrueExpr.OutValue != null &&
                expr.FalseExpr.OutValue != null)
            {
                expr.OutValue = ((bool)expr.Condition.OutValue) ? expr.TrueExpr.OutValue : expr.FalseExpr.OutValue;
            }
        }

        private void PostPrepareCheckedExprInference(AstCheckedExpr expr, InInfo inInfo, ref OutInfo outInfo)
        {
            // TODO: static overflow check? like 'checked(int.MaxValue + 1)' - would error in c# at comp time
            PostPrepareExprInference(expr.SubExpression, inInfo, ref outInfo);
            expr.OutType = expr.SubExpression.OutType;
            expr.OutValue = expr.SubExpression.OutValue;
        }

        private void PostPrepareSATExprInference(AstSATOfExpr expr, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(expr.TargetType, inInfo, ref outInfo);
            if (expr.ExprType == TokenType.KwSizeof || expr.ExprType == TokenType.KwAlignof)
            {
                expr.OutType = HapetType.CurrentTypeContext.GetIntType(4, true);
            }
            else if (expr.ExprType == TokenType.KwNameof)
            {
                expr.OutType = HapetType.CurrentTypeContext.StringTypeInstance;
            }
            else if (expr.ExprType == TokenType.KwTypeof)
            {
                // typeof handle here
                expr.OutType = HapetType.CurrentTypeContext.TypeTypeInstance;
            }
            else
            {
                // TODO: compiler error here
                Debug.Assert(false);
            }
        }

        private void PostPrepareLambdaExprInference(AstLambdaExpr expr, InInfo inInfo, ref OutInfo outInfo)
        {
            // should not be inferenced here !!!
            /// inferenced in <see cref="PostPrepareLambdaWithType"/>
        }

        private void PostPrepareNullableExprInference(AstNullableExpr expr, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(expr.SubExpression, inInfo, ref outInfo);
            if (expr.SubExpression.OutType is not StructType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, expr.SubExpression, [], ErrorCode.Get(CTEN.NullableNotStruct));
                return;
            }
            var nullableType = GetNullableType(expr.SubExpression, expr);
            expr.OutType = nullableType;
        }

        // statements
        private void PostPrepareAssignStmtInference(AstAssignStmt assignStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareNestedExprInference(assignStmt.Target, inInfo, ref outInfo);

            // cringe error when user tries to assign something directly into enum field
            if (assignStmt.Target.LeftPart != null && assignStmt.Target.LeftPart.OutType is EnumType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, assignStmt, [], ErrorCode.Get(CTEN.EnumFieldAssigned));
                return;
            }
            // pp assign value
            if (assignStmt.Value != null)
            {
                assignStmt.Value = PostPrepareVarValueAssign(assignStmt.Value, assignStmt.Target.OutType, inInfo, ref outInfo);
            }
            else
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, assignStmt, [], ErrorCode.Get(CTEN.NotExprInAssignment));
        }

        private void PostPrepareForStmtInference(AstForStmt forStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            if (forStmt.IsForeach)
            {
                var varDecl = forStmt.FirstArgument as AstVarDecl;
                PostPrepareExprInference(varDecl.Type, inInfo, ref outInfo);
                // TODO: check for VarType and infer properly

                var arg = forStmt.ForeachArgument;
                PostPrepareExprInference(arg, inInfo, ref outInfo);
                // TODO: check that inherited from IEnumerable
                //forStmt.ForeachArgument.OutType.IsInheritedFrom();
                // TODO: check that the first arg has the required type 

                var getEnumeratorId = new AstIdExpr("GetEnumerator");
                getEnumeratorId.SetDataFromStmt(arg);
                var getEnumeratorObject = new AstNestedExpr(arg.GetDeepCopy() as AstExpression, null);
                getEnumeratorObject.SetDataFromStmt(arg);
                var getEnumeratorCall = new AstCallExpr(getEnumeratorObject, getEnumeratorId);
                getEnumeratorCall.SetDataFromStmt(arg);
                var getEnumeratorVarVar = new AstIdExpr("var");
                getEnumeratorVarVar.SetDataFromStmt(arg);
                var getEnumeratorVar = new AstVarDecl(getEnumeratorVarVar, new AstIdExpr("__enumeratorHolder"), getEnumeratorCall);
                getEnumeratorVar.SetDataFromStmt(arg);
                PostPrepareExprInference(getEnumeratorVar, inInfo, ref outInfo);
                getEnumeratorVar.Scope.DefineDeclSymbol(getEnumeratorVar.Name, getEnumeratorVar);

                var moveNextId = new AstIdExpr("MoveNext");
                moveNextId.SetDataFromStmt(arg);
                var moveNextObjectId = new AstIdExpr("__enumeratorHolder");
                moveNextObjectId.SetDataFromStmt(arg);
                var moveNextObject = new AstNestedExpr(moveNextObjectId, null);
                moveNextObject.SetDataFromStmt(arg);
                var moveNextCall = new AstCallExpr(moveNextObject, moveNextId);
                PostPrepareExprInference(moveNextCall, inInfo, ref outInfo);

                var currentId = new AstIdExpr("Current");
                currentId.SetDataFromStmt(arg);
                var currentObjectId = new AstIdExpr("__enumeratorHolder");
                currentObjectId.SetDataFromStmt(arg);
                var currentObject = new AstNestedExpr(currentObjectId, null);
                currentObject.SetDataFromStmt(arg);
                var currentNested = new AstNestedExpr(currentId, currentObject);
                currentNested.SetDataFromStmt(arg);
                var assignName = new AstNestedExpr(varDecl.Name.GetDeepCopy() as AstIdExpr, null);
                assignName.SetDataFromStmt(varDecl);
                var currentAssign = new AstAssignStmt(assignName, currentNested);
                currentAssign.SetDataFromStmt(varDecl);
                PostPrepareExprInference(currentAssign, inInfo, ref outInfo);

                forStmt.ForeachGetEnumeratorVar = getEnumeratorVar;
                forStmt.ForeachMoveNextCall = moveNextCall;
                forStmt.ForeachCurrentAssign = currentAssign;
            }
            else
            {
                if (forStmt.FirstArgument != null)
                    PostPrepareExprInference(forStmt.FirstArgument, inInfo, ref outInfo);
                if (forStmt.SecondArgument != null)
                {
                    PostPrepareExprInference(forStmt.SecondArgument, inInfo, ref outInfo);

                    // error if it is not a bool type because it has to be
                    if (forStmt.SecondArgument.OutType is not BoolType)
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, forStmt.SecondArgument, [], ErrorCode.Get(CTEN.ExprIsNotBool));
                    }
                }
                if (forStmt.ThirdArgument != null)
                    PostPrepareExprInference(forStmt.ThirdArgument, inInfo, ref outInfo);
            }            

            PostPrepareExprInference(forStmt.Body, inInfo, ref outInfo);
        }

        private void PostPrepareWhileStmtInference(AstWhileStmt whileStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(whileStmt.Condition, inInfo, ref outInfo);

            // error if it is not a bool type because it has to be
            if (whileStmt.Condition.OutType is not BoolType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, whileStmt.Condition, [], ErrorCode.Get(CTEN.ExprIsNotBool));
            }

            PostPrepareExprInference(whileStmt.Body, inInfo, ref outInfo);
        }

        private void PostPrepareDoWhileStmtInference(AstDoWhileStmt whileStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(whileStmt.Condition, inInfo, ref outInfo);

            // error if it is not a bool type because it has to be
            if (whileStmt.Condition.OutType is not BoolType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, whileStmt.Condition, [], ErrorCode.Get(CTEN.ExprIsNotBool));
            }

            PostPrepareExprInference(whileStmt.Body, inInfo, ref outInfo);
        }

        private void PostPrepareIfStmtInference(AstIfStmt ifStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(ifStmt.Condition, inInfo, ref outInfo);

            // error if it is not a bool type because it has to be
            if (ifStmt.Condition.OutType is not BoolType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, ifStmt.Condition, [], ErrorCode.Get(CTEN.ExprIsNotBool));
            }

            PostPrepareExprInference(ifStmt.BodyTrue, inInfo, ref outInfo);
            if (ifStmt.BodyFalse != null)
                PostPrepareExprInference(ifStmt.BodyFalse, inInfo, ref outInfo);
        }

        private void PostPrepareSwitchStmtInference(AstSwitchStmt switchStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(switchStmt.SubExpression, inInfo, ref outInfo);

            // used to check that there are no more than 1 default case
            bool thereWasADefaultCase = false;

            foreach (var cc in switchStmt.Cases)
            {
                PostPrepareExprInference(cc, inInfo, ref outInfo);

                // calc default cases. if there are more than 1 - error
                if (cc.IsDefaultCase)
                {
                    if (thereWasADefaultCase)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, cc.Pattern, [], ErrorCode.Get(CTEN.MultipleDefaultCases));
                    thereWasADefaultCase = true;
                    continue; // do not check for pattern in default expr...
                }

                // trying to implicitly cast cast value into switch sub expr
                cc.Pattern = PostPrepareExpressionWithType(switchStmt.SubExpression.OutType, cc.Pattern);

                // check that the value is a const 
                if (cc.Pattern.OutValue == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, cc.Pattern, [], ErrorCode.Get(CTEN.NonConstantCaseValue));
                }
            }
        }

        private void PostPrepareCaseStmtInference(AstCaseStmt caseStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            if (!caseStmt.IsDefaultCase)
                PostPrepareExprInference(caseStmt.Pattern, inInfo, ref outInfo);

            if (!caseStmt.IsFallingCase)
                PostPrepareExprInference(caseStmt.Body, inInfo, ref outInfo);
        }

        private void PostPrepareReturnStmtInference(AstReturnStmt returnStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            var currentFunction = _currentParentStack.GetNearestParentFuncOrLambda();
            var currFuncRet = currentFunction is AstFuncDecl fnc ? fnc.Returns : (currentFunction as AstLambdaExpr).Returns;
            var currFuncLoc = currentFunction is AstFuncDecl fnc2 ? fnc2.Name : currentFunction;

            // handle weak return
            if (returnStmt.IsWeakReturn && currentFunction is AstLambdaExpr)
            {
                returnStmt.IsWeakReturn = false;
                // if it is not a void type - make a normal return
                if (currFuncRet.OutType is not VoidType)
                    returnStmt.ReturnExpression = returnStmt.WeakReturnStatement as AstExpression;
                // has to be handled by block expr
                else
                    outInfo.NeedToAddFromWeakReturn.Push(returnStmt.WeakReturnStatement);
                returnStmt.WeakReturnStatement = null;
            }

            if (returnStmt.ReturnExpression != null)
            {
                // if user tries to return smth but func ret type is void =^0
                if (currFuncRet.OutType is VoidType)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, currFuncLoc, [], ErrorCode.Get(CTEN.EmptyReturnExpected));
                    return;
                }

                // casting to func return type
                returnStmt.ReturnExpression = PostPrepareVarValueAssign(returnStmt.ReturnExpression, currFuncRet.OutType, inInfo, ref outInfo);
            }
            else if (returnStmt.ReturnExpression == null && currFuncRet.OutType is not VoidType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, returnStmt, [HapetType.AsString(currFuncRet.OutType)], ErrorCode.Get(CTEN.EmptyReturnStmt));
            }
        }

        private void PostPrepareAttributeStmtInference(AstAttributeStmt attrStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            string attrNameFlatten = attrStmt.AttributeName.TryFlatten(_compiler.MessageHandler, _currentSourceFile);
            AstIdExpr fullTypeAstId = new AstIdExpr(attrNameFlatten);
            fullTypeAstId.SetDataFromStmt(attrStmt.AttributeName);

            var saved = inInfo.MuteErrors;
            inInfo.MuteErrors = true;
            PostPrepareExprInference(fullTypeAstId, inInfo, ref outInfo);
            // not found by pure name - try to add 'Attribute' to the end
            if (fullTypeAstId.OutType == null)
            {
                fullTypeAstId.Name = $"{fullTypeAstId.Name}Attribute";
                PostPrepareExprInference(fullTypeAstId, inInfo, ref outInfo);
            }
            inInfo.MuteErrors = saved;

            // check that the attr ast was infered properly
            if (fullTypeAstId.OutType == null)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, attrStmt.AttributeName, [], ErrorCode.Get(CTEN.AttrNotFound));
                return;
            }

            // if infered normally
            var nstName = new AstNestedExpr(fullTypeAstId, null);
            nstName.SetDataFromStmt(fullTypeAstId, true);
            attrStmt.AttributeName = nstName;

            // check that this cringe is inherited from attribute cls
            if (!(attrStmt.AttributeName.OutType is ClassType ct && 
                ct.Declaration.InheritedFrom.Count > 0 && 
                ct.Declaration.InheritedFrom[0].OutType is ClassType clsT && 
                clsT.Declaration.NameWithNs == "System.Attribute"))
            {
                // check that the shite is inherited from 'System.Attribute'
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, attrStmt.AttributeName, [], ErrorCode.Get(CTEN.AttrNotInhFromAttr));
            }

            // getting all the fields of attribuute class decl
            var attrDeclFields = (attrStmt.AttributeName.OutType as ClassType)?.Declaration.Declarations.
                Where(x => x is AstVarDecl vd && !vd.SpecialKeys.Contains(TokenType.KwStatic)).
                Select(x => x as AstVarDecl).ToList();

            // there were problems before
            if (attrDeclFields == null)
                return;

            // check that not too much params
            if (attrStmt.Arguments.Count > attrDeclFields.Count)
            {
                var beg = attrStmt.Arguments[attrDeclFields.Count].Beginning;
                var end = attrStmt.Arguments[attrStmt.Arguments.Count - 1].Ending;
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, new Location(beg, end), [attrDeclFields.Count.ToString(), attrStmt.Arguments.Count.ToString()], ErrorCode.Get(CTEN.WrongAttrArgs));
            }

            for (int i = 0; i < attrDeclFields.Count; ++i)
            {
                var theAttrField = attrDeclFields[i];

                // check that param exists for the field 
                if (i < attrStmt.Arguments.Count)
                {
                    // inferrencing the param
                    var arg = attrStmt.Arguments[i];
                    PostPrepareExprInference(arg, inInfo, ref outInfo);

                    var a = arg.Expr;
                    // all attr params has to be const values
                    if (a.OutValue == null)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, a, [], ErrorCode.Get(CTEN.NonComptimeAttrArg));

                    // is going to error if they are different types :)
                    attrStmt.Arguments[i].Expr = PostPrepareExpressionWithType(theAttrField.Type.OutType, a);
                }
                else
                {
                    // this cringe is done because current attribute requires RequiredAttribute to be inferred
                    foreach (var aa in theAttrField.Attributes)
                    {
                        if (aa.AttributeName.OutType == null)
                        {
                            var savedSourceFile = _currentSourceFile;
                            _currentSourceFile = theAttrField.SourceFile;
                            PostPrepareAttributeStmtInference(aa, inInfo, ref outInfo);
                            _currentSourceFile = savedSourceFile;
                        }
                    }

                    // check if the field is required but there are no more params - error
                    var reqAttr = theAttrField.GetAttribute("System.RequiredAttribute");
                    if (reqAttr != null)
                    {
                        // there was a required attr and no param for the field - error
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, attrStmt.Ending, [theAttrField.Name.Name], ErrorCode.Get(CTEN.NonSpecifiedRequired));
                    }
                }
            }
        }

        private void PostPrepareBaseCtorStmtInference(AstBaseCtorStmt baseStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            // resolve args
            foreach (var a in baseStmt.Arguments)
            {
                PostPrepareExprInference(a, inInfo, ref outInfo);
            }
        }

        private void PostPrepareThrowStmtInference(AstThrowStmt throwStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            // not parsed properly
            if (throwStmt.ThrowExpression == null)
                return;
            PostPrepareExprInference(throwStmt.ThrowExpression, inInfo, ref outInfo);
            // TODO: check that out type is derived from System.Exception
        }

        private void PostPrepareTryCatchStmtInference(AstTryCatchStmt stmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(stmt.TryBlock, inInfo, ref outInfo);
            foreach (var c in stmt.CatchBlocks)
                PostPrepareExprInference(c, inInfo, ref outInfo);
            if (stmt.FinallyBlock != null)
                PostPrepareExprInference(stmt.FinallyBlock, inInfo, ref outInfo);
        }

        private void PostPrepareCatchStmtInference(AstCatchStmt stmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareParamInference(stmt.CatchParam, inInfo, ref outInfo);
            PostPrepareExprInference(stmt.CatchBlock, inInfo, ref outInfo);
        }

        private void PostPrepareGotoStmtInference(AstGotoStmt stmt, InInfo inInfo, ref OutInfo outInfo)
        {
            // need to check if the label even exists
            // search for switch-case stmt
            var currParent = stmt.NormalParent;
            while (currParent is not AstSwitchStmt && currParent != null)
                currParent = currParent.NormalParent;

            // check if parent even exists
            if (currParent == null)
            {
                // switch-case not found - error
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, stmt, [], ErrorCode.Get(CTEN.GotoIsNotInSwitchCase));
                return;
            }

            // go all over cases and search the case
            AstCaseStmt cs = null;
            foreach (var c in (currParent as AstSwitchStmt).Cases)
            {
                if (c.LabelForGoto != null && c.LabelForGoto == stmt.GotoLabel)
                {
                    cs = c;
                    break;
                }
            }

            // check if found the case
            if (cs == null)
            {
                // there is no case with that label - error
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, stmt, [stmt.GotoLabel], ErrorCode.Get(CTEN.GotoLabelNotFoundInCases));
                return;
            }

            stmt.CaseToGoInto = cs;
        }

        private AstExpression PostPrepareVarValueAssign(AstExpression value, HapetType targetType, InInfo inInfo, ref OutInfo outInfo, bool inferValue = true)
        {
            if (value is AstDefaultExpr defExpr)
            {
                if (defExpr.TypeForDefault != null)
                    PostPrepareExprInference(defExpr.TypeForDefault, inInfo, ref outInfo);
                // get the default value for the type (no need to infer)
                var defaultOfDefault = AstDefaultExpr.GetDefaultValueForType(targetType ?? defExpr.TypeForDefault.OutType, value, _compiler.MessageHandler);
                if (defaultOfDefault == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, value, [], ErrorCode.Get(CTEN.DefaultValueNotFound));
                    return value;
                }
                value = defaultOfDefault;
            }
            if (inferValue && value is not AstLambdaExpr)
            {
                // if it is not a default
                PostPrepareExprInference(value, inInfo, ref outInfo);
            }

            if (targetType != null)
                return PostPrepareExpressionWithType(targetType, value);
            return value;
        }

        private bool CheckIfCouldBeAccessed(AstStatement accessor, AstDeclaration accessee, InInfo inInfo)
        {
            if (inInfo.AllowAccessToEveryShite)
                return true;
            // could be accessed from everyone
            if (accessee.SpecialKeys.Contains(TokenType.KwPublic))
                return true;
            // built in also could be accessed from everywhere
            if (accessee is AstBuiltInTypeDecl)
                return true;
            if (accessee.Scope.IsParentOf(accessor.Scope))
                return true;
            // allow call of stor from everywhere. there is no way user can call it but we have to be allowed
            if (accessee is AstFuncDecl fnc && fnc.ClassFunctionType == ClassFunctionType.StaticCtor)
                return true;

            // TODO: check protected
            // TODO: check private protected
            // TODO: check protected internal

            if (accessee.SpecialKeys.Contains(TokenType.KwInternal))
            {
                // check just by root namespace names
                string asm1 = accessor.SourceFile.Namespace.Split(".").First();
                string asm2 = accessee.SourceFile.Namespace.Split(".").First();
                return asm1 == asm2;
            }

            // they are the same 
            if (accessee.SpecialKeys.Contains(TokenType.KwPrivate) || accessee.SpecialKeys.Contains(TokenType.KwUnreflected))
            {
                // this shite could be accessable in the same namespace
                if (accessee is AstClassDecl ||
                    accessee is AstStructDecl ||
                    accessee is AstEnumDecl ||
                    accessee is AstDelegateDecl)
                {
                    return accessor.SourceFile.Namespace == accessee.SourceFile.Namespace;
                }
                else
                {
                    // if the decl has child shite
                    if (accessee.SubScope != null)
                    {
                        if (accessee.SubScope.IsParentOf(accessor.Scope))
                            return true;
                    }

                    // if the decl is func of field 
                    // and accessed in the same class
                    var parent = accessee switch
                    {
                        AstVarDecl vd => vd.ContainingParent,
                        AstFuncDecl fd => fd.ContainingParent,
                        _ => null
                    };
                    if (parent != null)
                    {
                        return parent.SubScope.IsParentOf(accessor.Scope);
                    }

                    return false;
                }
            }

            if (accessee.SpecialKeys.Contains(TokenType.KwProtected))
            {
                var accParent = accessee.ContainingParent;
                var currParent = _currentParentStack.GetNearestParentClassOrStruct();
                return currParent.Type.OutType.IsInheritedFrom(accParent.Type.OutType as ClassType);
            }

            // could be a usual variable/param
            if (accessee is AstParamDecl ||
                (accessee is AstVarDecl vd2 && vd2.ContainingParent == null))
            {
                return true;
            }

            return false;
        }
    }
}
