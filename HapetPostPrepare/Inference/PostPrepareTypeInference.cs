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
using Newtonsoft.Json.Linq;

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

        private void PostPrepareFunctionInference(AstFuncDecl funcDecl, InInfo inInfo, ref OutInfo outInfo)
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
                    if (funcDecl.ContainingParent is AstClassDecl || funcDecl.ContainingParent is AstStructDecl || funcDecl.ContainingParent is AstGenericDecl)
                    {
                        // additional shite to handle explicit funcs and make them
                        // from 'Anime::Test.Func()' into 'Anime::Namespace.Test.Func()'
                        // so it would be easier sooner to infer some shite
                        string explicitInterfaceName = "";
                        string funcName = funcDecl.Name.Name;
                        string pureName = funcName.GetPureFuncName();
                        if (funcDecl.Name.AdditionalData != null)
                        {
                            // safe check - errored somewhere before
                            if (funcDecl.Name.AdditionalData.OutType == null)
                            {
                                OnExit();
                                return;
                            }
                            explicitInterfaceName = (funcDecl.Name.AdditionalData.OutType as ClassType).Declaration.Name.Name;
                            explicitInterfaceName += '.';
                        }

                        // it could already contain all the shite if the func is imported from another assembly :)
                        if (!funcDecl.Name.Name.Contains("::"))
                            // renaming func name from 'Anime' to 'Cls::Anime(int, float)'
                            newName = newName.GetCopy($"{funcDecl.ContainingParent.Name.Name}::{explicitInterfaceName}{pureName}{funcDecl.Parameters.GetParamsString()}");
                        scopeToDefine = funcDecl.ContainingParent.SubScope;
                    }
                    else if (funcDecl.ContainingParent is AstFuncDecl fncDeclParent)
                    {
                        // it could already contain all the shite if the func is imported from another assembly :)
                        if (!funcDecl.Name.Name.Contains("::"))
                            // renaming func name from 'Anime' to 'Anime(int, float)'
                            newName = newName.GetCopy($"{funcDecl.Name.Name}{funcDecl.Parameters.GetParamsString()}");
                        scopeToDefine = fncDeclParent.Body.SubScope;
                    }
                    else
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, funcDecl.Name, [], ErrorCode.Get(CTEN.StmtNotAllowedInThis));
                        OnExit();
                        return;
                    }

                    // if it is public func - it should be visible in the scope in which func's class is
                    scopeToDefine.DefineDeclSymbol(newName, funcDecl);
                    funcDecl.Name = newName;

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
                // inferring body
                if (funcDecl.Body != null)
                    PostPrepareBlockInference(funcDecl.Body, inInfo, ref outInfo);

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
                        Scope = funcDecl.BaseCtorCall.Scope,
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
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, varDecl, [], ErrorCode.Get(CTEN.VarVarNoIniter));
                    return;
                }
                else if (varDecl.Initializer.OutType is VoidType)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, varDecl, [], ErrorCode.Get(CTEN.VarVoidType));
                    return;
                }
                else if (varDecl.Initializer is AstDefaultExpr def2 && def2.TypeForDefault == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, varDecl, [], ErrorCode.Get(CTEN.VarDefaultType));
                    return;
                }
                else
                    varDecl.Type.OutType = varDecl.Initializer.OutType;
            }

            // pp assign value
            if (varDecl.Initializer != null)
                varDecl.Initializer = PostPrepareVarValueAssign(varDecl.Initializer, varDecl.Type.OutType, inInfo, ref outInfo, false);

            // special keys could not be allowed when the var is declared in BlockExpr
            if (!inInfo.AllowSpecialKeys)
            {
                foreach (var kk in varDecl.SpecialKeys)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, new Location(varDecl.Beginning, varDecl.Name.Ending), [kk.ToString()], ErrorCode.Get(CTEN.VarTokenNotAllowed));
                }
            }

            // check for const value that it is compile time evaluated
            if ((varDecl.Initializer == null || varDecl.Initializer.OutValue == null) && varDecl.SpecialKeys.Contains(TokenType.KwConst))
            {
                // if it is an impl of generic type - no need to error about it 
                // because probably non-deep copy was created, so do not care
                if (!(varDecl.ContainingParent.HasGenericTypes && varDecl.ContainingParent.IsImplOfGeneric))
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, varDecl.Name, [], ErrorCode.Get(CTEN.ConstValueNonComptime));
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

        private void PostPrepareExprInference(AstStatement expr, InInfo inInfo, ref OutInfo outInfo)
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
                case AstEmptyExpr:
                    break;
                case AstStringExpr stringExpr:
                    stringExpr.OutType = GetStringType(stringExpr);
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

                // skip literals
                case AstNumberExpr:
                //case AstStringExpr:
                case AstBoolExpr:
                case AstCharExpr:
                case AstNullExpr:
                    break;

                default:
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, expr, [expr.AAAName], ErrorCode.Get(CTEN.StmtNotImplemented));
                        break;
                    }
            }
        }

        private void PostPrepareBlockInference(AstBlockExpr blockExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            var prevBlock = _currentBlock;
            _currentBlock = blockExpr;
            // go all over the statements
            foreach (var stmt in blockExpr.Statements.ToList())
            {
                if (stmt == null)
                    continue;
                PostPrepareExprInference(stmt, inInfo, ref outInfo);
            }

            _currentBlock = prevBlock;
        }

        private void PostPrepareUnaryExprInference(AstUnaryExpr unExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // TODO: check for the right size for an existance value (compiletime evaluated) and do some shite (set unExpr OutValue)
            PostPrepareExprInference(unExpr.SubExpr as AstExpression, inInfo, ref outInfo);

            var operators = unExpr.Scope.GetUnaryOperators(unExpr.Operator, (unExpr.SubExpr as AstExpression).OutType);
            if (operators.Count == 0)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, unExpr, 
                    [unExpr.Operator, HapetType.AsString((unExpr.SubExpr as AstExpression).OutType)], ErrorCode.Get(CTEN.UndefOpForType));
            }
            else if (operators.Count > 1)
            {
                // TODO: tell em where are the operators defined
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, unExpr, 
                    [unExpr.Operator, HapetType.AsString((unExpr.SubExpr as AstExpression).OutType)], ErrorCode.Get(CTEN.TooManyOpsForType));
            }
            else
            {
                unExpr.ActualOperator = operators[0];
                unExpr.OutType = unExpr.ActualOperator.ResultType;

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
            // resolve the actual operator in the current scope
            PostPrepareExprInference(binExpr.Left as AstExpression, inInfo, ref outInfo);
            PostPrepareExprInference(binExpr.Right as AstExpression, inInfo, ref outInfo);

            var operators = binExpr.Scope.GetBinaryOperators(binExpr.Operator, (binExpr.Left as AstExpression).OutType, (binExpr.Right as AstExpression).OutType);
            if (operators.Count == 0)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, binExpr, 
                    [binExpr.Operator, 
                    HapetType.AsString((binExpr.Left as AstExpression).OutType), 
                    HapetType.AsString((binExpr.Right as AstExpression).OutType)], 
                    ErrorCode.Get(CTEN.BinUndefOpForTypes));
            }
            else if (operators.Count > 1)
            {
                // TODO: tell em where are the operators defined
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, binExpr, 
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
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, rightExpr,
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
                                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, leftExpr, [binExpr.ActualOperator.Name], ErrorCode.Get(CTEN.OpUsedWithVoidPtr));
                                    var parent = rightExpr.NormalParent;
                                    var mulK = new AstNumberExpr((NumberData)ptrT.TargetType.GetSize(), null, null, rightExpr);
                                    SetScopeAndParent(mulK, parent);
                                    rightExpr = new AstBinaryExpr("*", rightExpr, mulK, rightExpr);
                                    SetScopeAndParent(rightExpr, parent);
                                    PostPrepareExprInference(rightExpr, inInfo, ref outInfo);
                                    binExpr.Right = rightExpr;
                                }
                                else if (rightExpr.OutType is PointerType ptrT2)
                                {
                                    // error if bin op with void*
                                    if (ptrT2.TargetType is VoidType)
                                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, rightExpr, [binExpr.ActualOperator.Name], ErrorCode.Get(CTEN.OpUsedWithVoidPtr));
                                    var parent = leftExpr.NormalParent;
                                    var mulK = new AstNumberExpr((NumberData)ptrT2.TargetType.GetSize(), null, null, leftExpr);
                                    SetScopeAndParent(mulK, parent);
                                    leftExpr = new AstBinaryExpr("*", leftExpr, mulK, leftExpr);
                                    SetScopeAndParent(leftExpr, parent);
                                    PostPrepareExprInference(leftExpr, inInfo, ref outInfo);
                                    binExpr.Left = leftExpr;
                                }
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
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text,
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
            // prepare the right side
            PostPrepareExprInference(newExpr.TypeName, inInfo, ref outInfo);
            // the type of newExpr is the same as the type of its name expr
            newExpr.OutType = newExpr.TypeName.OutType;

            // error if they trying to create an instance from interface of an abstract class
            if (newExpr.TypeName.OutType is ClassType clsType && 
                (clsType.Declaration.IsInterface || 
                clsType.Declaration.SpecialKeys.Contains(TokenType.KwAbstract)))
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, newExpr, [], ErrorCode.Get(CTEN.CreateInterfOrAbsCls));
            }

            foreach (var a in newExpr.Arguments)
            {
                PostPrepareExprInference(a, inInfo, ref outInfo);
            }
        }

        private void PostPrepareArgumentExprInference(AstArgumentExpr argumentExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(argumentExpr.Expr, inInfo, ref outInfo);

            if (argumentExpr.Name != null)
            {
                // WARN: do not infer the arg name. it has to be errored while candidating
                // PostPrepareExprInference(argumentExpr.Name, inInfo, ref outInfo);
            }

            // the argument type is the same as its expr type
            argumentExpr.OutType = argumentExpr.Expr.OutType;
            // if the value could be evaluated at the compile time
            if (argumentExpr.Expr.OutValue != null)
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
            }
            else
            {
                castExpr.OutType = castExpr.SubExpression.OutType;
            }
            castExpr.OutValue = castExpr.SubExpression.OutValue; // WARN: is it ok just to pass the value?
        }

        private void PostPrepareNestedExprInference(AstNestedExpr nestExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // the var is used to check when static/const field is accessed from an object
            bool accessingFromAnObject = false;

            bool foundNs = false;
            // normalizing types with their namespaces
            InternalNormalizeLeftPartIfItIsANamespaceWithType(nestExpr, ref foundNs);

            if (nestExpr.LeftPart == null)
            {
                PostPrepareExprInference(nestExpr.RightPart, inInfo, ref outInfo);
                nestExpr.OutType = nestExpr.RightPart.OutType;
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
                    }, null, nestExpr)
                    {
                        Location = nestExpr.Location,
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

                if (leftSideDecl == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, nestExpr.LeftPart, [], ErrorCode.Get(CTEN.ExprNotClassOrStruct));
                    return;
                }

                // here could only be an AstIdExpr because AstCallExpr and AstExpression would be in 'if' block upper
                if (nestExpr.RightPart is not AstIdExpr idExpr)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, nestExpr.RightPart, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                    return;
                }

                // searching for the symbol in the class/struct
                PostPrepareIdentifierInference(idExpr, inInfo, ref outInfo, leftSideDecl);
                var smbl = idExpr.FindSymbol;
                if (smbl is DeclSymbol typed)
                {
                    idExpr.OutType = typed.Decl.Type.OutType;
                    nestExpr.OutType = idExpr.OutType;
                    nestExpr.OutValue = idExpr.OutValue;

                    // check if the var is a static/const field and user is accessing it from an object
                    if (typed.Decl is AstVarDecl varDecl && (varDecl.SpecialKeys.Contains(TokenType.KwStatic) || varDecl.SpecialKeys.Contains(TokenType.KwConst)) && accessingFromAnObject) // if accessing from an object - give em a warning :)
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTWN.StaticFieldFromObject), null, HapetFrontend.Entities.ReportType.Warning);
                    }
                }
                else
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [HapetType.AsString(nestExpr.LeftPart.OutType)], ErrorCode.Get(CTEN.SymbolNotFoundInType));
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
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, defaultExpr, [], ErrorCode.Get(CTEN.DefaultWasNotInfered));
                return;
            }
            PostPrepareExprInference(defaultExpr.TypeForDefault, inInfo, ref outInfo);
            defaultExpr.OutType = defaultExpr.TypeForDefault.OutType;
        }

        private void PostPrepareDefaultGenericExprInference(AstDefaultGenericExpr defaultExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            if (defaultExpr.TypeForDefault == null)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, defaultExpr, [], ErrorCode.Get(CTEN.DefaultWasNotInfered));
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
            // TODO: you can check if the size is available at compile time and create the array on stack

            PostPrepareExprInference(arrayExpr.TypeName, inInfo, ref outInfo);

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
                var ind = declItself.GetDeclarations().
                    FirstOrDefault(x => 
                    {
                        if (x is not AstIndexerDecl ind)
                            return false;
                        var cstResult = new CastResult();
                        _compiler.TryCastExpr(ind.IndexerParameter.Type.OutType, arrayAccExpr.ParameterExpr, cstResult);
                        return cstResult.CouldBeCasted;
                    });

                if (ind is AstIndexerDecl indDecl)
                {
                    arrayAccExpr.OutType = indDecl.Type.OutType;
                    arrayAccExpr.IndexerDecl = indDecl;
                    return; // everything is ok :)
                }
            }

            if (arrayAccExpr.ParameterExpr.OutType is not IntType)
            {
                // error here? i cannot access array if it is not an int type
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, arrayAccExpr.ParameterExpr, [], ErrorCode.Get(CTEN.ArrayIndexNotInt));
            }

            HapetType outType = null;
            if (arrayAccExpr.ObjectName.OutType is ArrayType arrayType)
                outType = arrayType.TargetType;
            else if (arrayAccExpr.ObjectName.OutType is StringType)
                outType = HapetType.CurrentTypeContext.CharTypeInstance; // TODO: mb non default could be here? idk :)
            else if (arrayAccExpr.ObjectName.OutType is PointerType ptrType)
                outType = ptrType.TargetType;
            else
            {
                // error because expected an array 
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, arrayAccExpr.ObjectName, [], ErrorCode.Get(CTEN.NonStringOrArrayIndexed));
            }
            arrayAccExpr.OutType = outType;
        }

        private void PostPrepareTernaryExprInference(AstTernaryExpr expr, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(expr.Condition, inInfo, ref outInfo);
            if (expr.Condition.OutType is not BoolType) 
            {
                // error
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, expr.Condition, [], ErrorCode.Get(CTEN.ExprIsNotBool));
            }

            PostPrepareExprInference(expr.TrueExpr, inInfo, ref outInfo);
            PostPrepareExprInference(expr.FalseExpr, inInfo, ref outInfo);
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
                    // TODO: error that the types are not connected to each other
                }
            }

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

        // statements
        private void PostPrepareAssignStmtInference(AstAssignStmt assignStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareNestedExprInference(assignStmt.Target, inInfo, ref outInfo);

            // cringe error when user tries to assign something directly into enum field
            if (assignStmt.Target.LeftPart != null && assignStmt.Target.LeftPart.OutType is EnumType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, assignStmt, [], ErrorCode.Get(CTEN.EnumFieldAssigned));
                return;
            }
            // pp assign value
            if (assignStmt.Value != null)
            {
                assignStmt.Value = PostPrepareVarValueAssign(assignStmt.Value, assignStmt.Target.OutType, inInfo, ref outInfo);
            }
            else
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, assignStmt, [], ErrorCode.Get(CTEN.NotExprInAssignment));
        }

        private void PostPrepareForStmtInference(AstForStmt forStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            if (forStmt.FirstArgument != null)
                PostPrepareExprInference(forStmt.FirstArgument, inInfo, ref outInfo);
            if (forStmt.SecondArgument != null)
            {
                PostPrepareExprInference(forStmt.SecondArgument, inInfo, ref outInfo);

                // error if it is not a bool type because it has to be
                if (forStmt.SecondArgument.OutType is not BoolType)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, forStmt.SecondArgument, [], ErrorCode.Get(CTEN.ExprIsNotBool));
                }
            }
            if (forStmt.ThirdArgument != null)
                PostPrepareExprInference(forStmt.ThirdArgument, inInfo, ref outInfo);

            PostPrepareExprInference(forStmt.Body, inInfo, ref outInfo);
        }

        private void PostPrepareWhileStmtInference(AstWhileStmt whileStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(whileStmt.Condition, inInfo, ref outInfo);

            // error if it is not a bool type because it has to be
            if (whileStmt.Condition.OutType is not BoolType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, whileStmt.Condition, [], ErrorCode.Get(CTEN.ExprIsNotBool));
            }

            PostPrepareExprInference(whileStmt.Body, inInfo, ref outInfo);
        }

        private void PostPrepareIfStmtInference(AstIfStmt ifStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(ifStmt.Condition, inInfo, ref outInfo);

            // error if it is not a bool type because it has to be
            if (ifStmt.Condition.OutType is not BoolType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, ifStmt.Condition, [], ErrorCode.Get(CTEN.ExprIsNotBool));
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
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, cc.Pattern, [], ErrorCode.Get(CTEN.MultipleDefaultCases));
                    thereWasADefaultCase = true;
                    continue; // do not check for pattern in default expr...
                }

                // trying to implicitly cast cast value into switch sub expr
                cc.Pattern = PostPrepareExpressionWithType(switchStmt.SubExpression.OutType, cc.Pattern);

                // check that the value is a const 
                if (cc.Pattern.OutValue == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, cc.Pattern, [], ErrorCode.Get(CTEN.NonConstantCaseValue));
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
            var currentFunction = _currentParentStack.GetNearestParentFunc();

            if (returnStmt.ReturnExpression != null)
            {
                // if user tries to return smth but func ret type is void =^0
                if (currentFunction.Returns.OutType is VoidType)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, currentFunction.Name, [], ErrorCode.Get(CTEN.EmptyReturnExpected));
                    return;
                }

                // casting to func return type
                returnStmt.ReturnExpression = PostPrepareVarValueAssign(returnStmt.ReturnExpression, currentFunction.Returns.OutType, inInfo, ref outInfo);
            }
            else if (returnStmt.ReturnExpression == null && currentFunction.Returns.OutType is not VoidType)
            {
                // TODO: better return stmts checks. like in if/else blocks and so on
                if (returnStmt.Location == null)
                {
                    // it is a manually added 'return' statement
                    var theFunc = returnStmt.FindContainingFunction();
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, theFunc.Name, [HapetType.AsString(currentFunction.Returns.OutType)], ErrorCode.Get(CTEN.NotEnoughReturns));
                }
                else
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, returnStmt, [HapetType.AsString(currentFunction.Returns.OutType)], ErrorCode.Get(CTEN.EmptyReturnStmt));
            }
        }

        private void PostPrepareAttributeStmtInference(AstAttributeStmt attrStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            // purified type string with namespace in it!
            // we need this so when saving the attributes into metafile 
            // we would know namespace of the attribute and so on.
            // (kostyl?)
            var newTypeAst = attrStmt.AttributeName.GetTypeAstId(_compiler.MessageHandler, _currentSourceFile);
            PostPrepareExprInference(newTypeAst, inInfo, ref outInfo);
            attrStmt.AttributeName.SetTypeAstId(newTypeAst);

            // check that the attr ast was infered properly
            if (attrStmt.AttributeName.OutType == null)
                return;

            // check that this cringe is inherited from attribute cls
            if (!(attrStmt.AttributeName.OutType is ClassType ct && 
                ct.Declaration.InheritedFrom.Count > 0 && 
                ct.Declaration.InheritedFrom[0].TryFlatten(null, null) == "System.Attribute"))
            {
                // check that the shite is inherited from 'System.Attribute'
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, attrStmt.AttributeName, [], ErrorCode.Get(CTEN.AttrNotInhFromAttr));
            }

            // getting all the fields of attribuute class decl
            var attrDeclFields = (attrStmt.AttributeName.OutType as ClassType).Declaration.Declarations.
                Where(x => x is AstVarDecl vd && !vd.SpecialKeys.Contains(TokenType.KwStatic)).
                Select(x => x as AstVarDecl).ToList();

            // check that not too much params
            if (attrStmt.Arguments.Count > attrDeclFields.Count)
            {
                var beg = attrStmt.Arguments[attrDeclFields.Count].Beginning;
                var end = attrStmt.Arguments[attrStmt.Arguments.Count - 1].Ending;
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, new Location(beg, end), [attrDeclFields.Count.ToString(), attrStmt.Arguments.Count.ToString()], ErrorCode.Get(CTEN.WrongAttrArgs));
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
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, a, [], ErrorCode.Get(CTEN.NonComptimeAttrArg));

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
                    string reqAttrName = "System.RequiredAttribute";
                    var reqAttr = theAttrField.Attributes.FirstOrDefault(x => x.AttributeName.TryFlatten(_compiler.MessageHandler, _currentSourceFile) == reqAttrName);
                    if (reqAttr != null)
                    {
                        // there was a required attr and no param for the field - error
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, attrStmt.Ending, [theAttrField.Name.Name], ErrorCode.Get(CTEN.NonSpecifiedRequired));
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

        private AstExpression PostPrepareVarValueAssign(AstExpression value, HapetType targetType, InInfo inInfo, ref OutInfo outInfo, bool inferValue = true)
        {
            if (value is AstDefaultExpr)
            {
                // get the default value for the type (no need to infer)
                var defaultOfDefault = AstDefaultExpr.GetDefaultValueForType(targetType, value, _compiler.MessageHandler);
                if (defaultOfDefault == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, value, [], ErrorCode.Get(CTEN.DefaultValueNotFound));
                    return value;
                }
                value = defaultOfDefault;
            }
            // do not infer the expr if target is a delegate
            else if (targetType is not DelegateType)
            {
                if (inferValue)
                {
                    // if it is not a default
                    PostPrepareExprInference(value, inInfo, ref outInfo);
                }
            }

            if (targetType != null)
                return PostPrepareExpressionWithType(targetType, value);
            return value;
        }

        private bool CheckIfCouldBeAccessed(AstStatement accessor, AstDeclaration accessee)
        {
            // could be accessed from everyone
            if (accessee.SpecialKeys.Contains(TokenType.KwPublic))
                return true;

            // built in also could be accessed from everywhere
            if (accessee is AstBuiltInTypeDecl)
                return true;

            // if it is a nested - check that is it accessed within the parent class
            if (accessee.IsNestedDecl && accessee.Scope.IsParentOf(accessor.Scope))
            {
                return true;
            }

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
                        return parent.Scope.IsParentOf(accessor.Scope);
                    }

                    return false;
                }
            }

            // could be a usual variable/param
            if (accessee is AstParamDecl ||
                (accessee is AstVarDecl vd2 && vd2.ContainingParent == null))
            {
                return true;
            }

            // allow access to all struct fields!!!
            if (accessee is AstVarDecl && accessee.ContainingParent is AstStructDecl)
            {
                return true;
            }

            return false;
        }
    }
}
