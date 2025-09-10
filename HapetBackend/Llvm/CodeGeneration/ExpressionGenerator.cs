using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare;
using LLVMSharp.Interop;
using System;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private LLVMValueRef GenerateExpressionCode(AstStatement expr, bool getPtr = false)
        {
            // if the value already evaluated (usually literals) 
            if (expr is AstExpression realExpr && realExpr.OutValue != null &&
                (realExpr is AstStringExpr || realExpr is AstNumberExpr || 
                realExpr is AstBoolExpr || realExpr is AstCharExpr))
            {
                var result = HapetValueToLLVMValue(realExpr.OutType, realExpr.OutValue, getPtr);
                return result;
            }

            switch (expr)
            {
                // special case at least for 'for' loop
                // when 'for (int i = 0;...)' where 'int i' 
                // would not be handled by blockExpr
                case AstVarDecl varDecl: GenerateVarDeclCode(varDecl); return null;

                case AstBlockExpr blockExpr: return GenerateBlockExprCode(blockExpr);
                case AstUnaryIncDecExpr unExpr: return GenerateUnaryIncDecExprCode(unExpr, getPtr);
                case AstUnaryExpr unExpr2: return GenerateUnaryExprCode(unExpr2, getPtr);
                case AstBinaryExpr binExpr: return GenerateBinaryExprCode(binExpr, getPtr);
                case AstPointerExpr pointerExpr: return GeneratePointerExprCode(pointerExpr, getPtr);
                case AstAddressOfExpr addrExpr: return GenerateAddressOfExprCode(addrExpr);
                case AstIdExpr idExpr: return GenerateIdExpr(idExpr, getPtr);
                case AstNewExpr newExpr: return GenerateNewExpr(newExpr, getPtr);
                case AstCallExpr callExpr: return GenerateCallExpr(callExpr, getPtr);
                case AstArgumentExpr argExpr: return GenerateArgumentExpr(argExpr);
                case AstCastExpr castExpr: return GenerateCastExpr(castExpr, getPtr);
                case AstNestedExpr nestExpr: return GenerateNestedExpr(nestExpr, getPtr);
                case AstArrayCreateExpr arrayCreateExpr: return GenerateArrayCreateExprCode(arrayCreateExpr, getPtr);
                case AstArrayAccessExpr arrayAccessExpr: return GenerateArrayAccessExprCode(arrayAccessExpr, getPtr);
                case AstTernaryExpr ternaryExpr: return GenerateTernaryExprCode(ternaryExpr);
                case AstCheckedExpr checkedExpr: return GenerateCheckedExprCode(checkedExpr);
                case AstSATOfExpr satExpr: return GenerateSATExprCode(satExpr, getPtr);
                case AstEmptyStructExpr emptyStructExpr: return GenerateEmptyStructExprCode(emptyStructExpr);
                case AstLambdaExpr lambdaExpr: return GenerateLambdaExprCode(lambdaExpr);

                case AstNullExpr nullExpr: return GenerateNullExprCode(nullExpr);

                // statements
                case AstAssignStmt assignStmt: GenerateAssignStmt(assignStmt); return null;
                case AstForStmt forStmt: GenerateForStmt(forStmt); return null;
                case AstWhileStmt whileStmt: GenerateWhileStmt(whileStmt); return null;
                case AstDoWhileStmt doWhileStmt: GenerateDoWhileStmt(doWhileStmt); return null;
                case AstIfStmt ifStmt: GenerateIfStmt(ifStmt); return null;
                case AstSwitchStmt switchStmt: GenerateSwitchStmt(switchStmt); return null;
                case AstBreakContStmt breakContStmt: GenerateBreakContStmt(breakContStmt); return null;
                case AstReturnStmt returnStmt: GenerateReturnStmt(returnStmt); return null;
                case AstBaseCtorStmt baseStmt: GenerateBaseCtorStmt(baseStmt); return null;
                case AstThrowStmt throwStmt: GenerateThrowStmt(throwStmt); return null;
                case AstTryCatchStmt tryCatchStmt: GenerateTryCatchStmt(tryCatchStmt); return null;
                case AstGotoStmt gotoStmt: GenerateGotoStmt(gotoStmt); return null;

                default:
                    {
                        _messageHandler.ReportMessage(_currentSourceFile.Text, expr, [expr.AAAName], ErrorCode.Get(CTEN.StmtNotImplemented));
                        return default;
                    }
            }
        }

        private LLVMValueRef GenerateBlockExprCode(AstBlockExpr blockExpr)
        {
            LLVMValueRef result = default;
            foreach (var stmt in blockExpr.Statements)
            {
                if (stmt == null)
                    continue;

                // skip nested func
                if (stmt is not AstFuncDecl)
                    GenerateExpressionCode(stmt);

                // no need to generate anything after throw, goto
                // throw gen will add unreachable
                if (stmt is AstThrowStmt || stmt is AstGotoStmt)
                    break;
            }
            return result;
        }

        private unsafe LLVMValueRef GenerateUnaryExprCode(AstUnaryExpr unExpr, bool getPtr = false)
        {
            LLVMValueRef toReturn = default;
            if (unExpr.ActualOperator is BuiltInUnaryOperator builtInOp)
            {
                var expr = (unExpr.SubExpr as AstExpression);
                var value = GenerateExpressionCode(expr);
                // return if the value was not properly generated
                if (value == default)
                    return default;

                // special case for tilda generation
                // because it requires me to know the size of 
                // type that is going to be tilded
                if (unExpr.Operator == "~")
                {
                    using var marshaledName = new MarshaledString("tilded".AsSpan());
                    var mask = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(unExpr.OutType), unchecked((ulong)-1));
                    var val = LLVM.BuildXor(_builder, value, mask, marshaledName);
                }
                else
                {
                    var uo = GetUnOp(builtInOp);
                    var val = uo(_builder, value, "unOp");
                    toReturn = val;
                }
            }
            else if (unExpr.ActualOperator is UserDefinedUnaryOperator userDef)
            {
                var expr = (unExpr.SubExpr as AstExpression);
                var value = GenerateExpressionCode(expr);
                // return if the value was not properly generated
                if (value == default)
                    return default;

                var fncType = _typeMap[userDef.Function];
                var fncValue = _valueMap[userDef.Function.Declaration.Symbol];

                toReturn = _builder.BuildCall2(fncType, fncValue, new LLVMValueRef[] { value }, "unOp");
            }
            if (toReturn != default)
            {
                if (getPtr)
                {
                    LLVMValueRef varPtr = CreateLocalVariable(unExpr.OutType, "unRetHolder");
                    _builder.BuildStore(toReturn, varPtr);
                    return varPtr;
                }
                return toReturn;
            }
            _messageHandler.ReportMessage(_currentSourceFile.Text, unExpr, [unExpr.ToString()], ErrorCode.Get(CTEN.StmtNotImplemented));
            return default;
        }

        private LLVMValueRef GenerateUnaryIncDecExprCode(AstUnaryIncDecExpr unExpr, bool getPtr = false)
        {
            LLVMValueRef toReturn = default;
            if (unExpr.ActualOperator is BuiltInUnaryOperator builtInOp)
            {
                LLVMValueRef toAssignBefore;
                LLVMValueRef toAssignAfter;
                var expr = (unExpr.SubExpr as AstExpression);
                bool isPropa = expr is AstNestedExpr nst && nst.RightPart is AstCallExpr cp && cp.FuncName.OutType is FunctionType fnc && fnc.Declaration.IsPropertyFunction;

                LLVMValueRef valuePtr = default;
                AstCallExpr copiedCall = null;
                // check for property function call
                if (isPropa)
                {
                    // this is a special case for property function call
                    // calling get func
                    toAssignBefore = GenerateExpressionCode(expr);

                    var callProp = (expr as AstNestedExpr).RightPart as AstCallExpr;
                    var getFunc = callProp.FuncName.OutType as FunctionType;
                    // making copy of call for set function
                    copiedCall = callProp.GetDeepCopy() as AstCallExpr;
                    var setFunc = (getFunc.Declaration.NormalParent as AstPropertyDecl).SetFunction;
                    copiedCall.FuncName.OutType = setFunc.Type.OutType;
                    copiedCall.FuncName.FindSymbol = setFunc.Symbol;
                    copiedCall.OutType = HapetType.CurrentTypeContext.VoidTypeInstance;
                }
                else
                {
                    valuePtr = GenerateExpressionCode(expr, true);
                    toAssignBefore = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType), valuePtr, "loadedDec");
                }

                // making ++/--
                if (expr.OutType is PointerType ptrT)
                {
                    // special case for ptr type with GEP magics
                    int valToGep = unExpr.Operator == "++" ? 1 : -1;
                    toAssignAfter = _builder.BuildGEP2(HapetTypeToLLVMType(ptrT.TargetType), toAssignBefore, [LLVMValueRef.CreateConstInt(_context.Int32Type, (uint)valToGep)]);
                }
                else
                {
                    // if it is just a numbers - just add
                    var tpToAdd = expr.OutType;
                    var bo = SearchBinOp(unExpr.Operator == "++" ? "+" : "-", expr.OutType, tpToAdd);
                    toAssignAfter = bo(_builder, toAssignBefore, LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(tpToAdd), 1), "unOp");
                }

                // assigning
                if (isPropa)
                {
                    GenerateCallExpr(copiedCall, false, [toAssignAfter]);
                }
                else
                {
                    AssignToVar(valuePtr, toAssignAfter);
                }

                LLVMValueRef val;
                if (unExpr.IsPrefix)
                    val = toAssignAfter;
                else
                    val = toAssignBefore;
                toReturn = val;
            }
            else if (unExpr.ActualOperator is UserDefinedUnaryOperator userDef)
            {
                var expr = (unExpr.SubExpr as AstExpression);

                LLVMValueRef val;
                if (unExpr.IsPrefix)
                {
                    MakeOperation();
                    val = GenerateExpressionCode(expr);
                }
                else
                {
                    val = GenerateExpressionCode(expr);
                    MakeOperation();
                }
                toReturn = val;

                void MakeOperation()
                {
                    var value = GenerateExpressionCode(expr);
                    var fncType = _typeMap[userDef.Function];
                    var fncValue = _valueMap[userDef.Function.Declaration.Symbol];
                    _builder.BuildCall2(fncType, fncValue, new LLVMValueRef[] { value });
                }
            }
            if (toReturn != default)
            {
                if (getPtr)
                {
                    LLVMValueRef varPtr = CreateLocalVariable(unExpr.OutType, "decRetHolder");
                    _builder.BuildStore(toReturn, varPtr);
                    return varPtr;
                }
                return toReturn;
            }
            _messageHandler.ReportMessage(_currentSourceFile.Text, unExpr, [unExpr.ToString()], ErrorCode.Get(CTEN.StmtNotImplemented));
            return default;
        }

        private LLVMValueRef GenerateBinaryExprCode(AstBinaryExpr binExpr, bool getPtr = false)
        {
            LLVMValueRef toReturn = default;
            if (binExpr.ActualOperator is BuiltInBinaryOperator builtInOp)
            {
                // CRINGE :) special cases for as/is/in
                switch (binExpr.ActualOperator.Name)
                {
                    case "as":
                        {
                            var leftExpr = (binExpr.Left as AstExpression);
                            var left = GenerateExpressionCode(leftExpr);
                            // return if the value was not properly generated
                            if (left == default)
                                return default;

                            var rightExpr = (binExpr.Right as AstExpression);

                            if (rightExpr.OutType is PointerType pt1 && pt1.TargetType is ClassType rt)
                            {
                                ClassType leftType;
                                ClassType rightType = rt;
                                if (leftExpr.OutType is PointerType pt2 && pt2.TargetType is ClassType clsT)
                                {
                                    leftType = clsT;
                                }
                                else
                                {
                                    // when smth like 'valueTyped as ICringeCock'
                                    toReturn = CreateCast(_builder, left, leftExpr.OutType, pt1);
                                    break;
                                }

                                // you would ask - wtf is anyIsInterface?
                                // then I would say:
                                // https://habr.com/ru/articles/882888/
                                // this shite has no compile time errors in c#...
                                bool anyIsInterface = rightType.Declaration.IsInterface || leftType.Declaration.IsInterface;
                                bool isUpcast = leftType.IsInheritedFrom(rightType);
                                // check upcast
                                if (!isUpcast || anyIsInterface)
                                {
                                    // swap them and check inheritance
                                    bool isDownCast = rightType.IsInheritedFrom(leftType);
                                    if (isDownCast || anyIsInterface)
                                    {
                                        var castTypeInfo = _builder.BuildLoad2(GetTypeType(), _typeDictionary[rightType], "typeLoaded");
                                        var castTypeNull = LLVMValueRef.CreateConstPointerNull(HapetTypeToLLVMType(pt1));
                                        var casted = _builder.BuildBitCast(left, HapetTypeToLLVMType(pt1), "castedAs");

                                        // WARN: hard cock
                                        var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("TypeConverter"));
                                        DeclSymbol downcasterSymbol;
                                        if (rightType.Declaration.IsInterface)
                                            downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("CanBeDowncastedInterface")) as DeclSymbol;
                                        else
                                            downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("CanBeDowncasted")) as DeclSymbol;
                                        var downcasterFunc = _valueMap[downcasterSymbol];
                                        LLVMTypeRef funcType = _typeMap[downcasterSymbol.Decl.Type.OutType];
                                        var canBeDowncasted = _builder.BuildCall2(funcType, downcasterFunc, new LLVMValueRef[] { left, castTypeInfo }, "canBeDowncasted");
                                        toReturn = _builder.BuildSelect(canBeDowncasted, casted, castTypeNull, "castResult");
                                        break;
                                    }
                                }
                                else
                                {
                                    // just bitcast when upcast shite
                                    toReturn = _builder.BuildBitCast(left, HapetTypeToLLVMType(rightExpr.OutType), "castedAs");
                                    break;
                                }
                                break;
                            }
                            else
                            {
                                ClassType leftType;
                                StructType rightType = rightExpr.OutType as StructType;
                                if (leftExpr.OutType is ClassType clsT)
                                {
                                    leftType = clsT;
                                }
                                else
                                {
                                    // TODO: error in PP. user tried to do smth like 
                                    // valueType as SomeStructType
                                    return default;
                                }

                                // check inheritance
                                bool isDownCast = rightType.IsInheritedFrom(leftType);
                                if (isDownCast)
                                {
                                    toReturn = CreateStructCastFromObject(left, rightType, false);
                                    break;
                                }
                            }
                            _messageHandler.ReportMessage(_currentSourceFile.Text, binExpr, [HapetType.AsString(leftExpr.OutType), HapetType.AsString(rightExpr.OutType)], ErrorCode.Get(CTEN.TypeCouldNotBeConverted));
                            return default;
                        }
                    case "is":
                        {
                            var leftExpr = (binExpr.Left as AstExpression);
                            var left = GenerateExpressionCode(leftExpr);
                            // return if the value was not properly generated
                            if (left == default)
                                return default;

                            var rightExpr = (binExpr.Right as AstExpression);

                            toReturn = CheckIsCouldBeCasted(left, leftExpr.OutType, rightExpr.OutType, binExpr.IsNot, binExpr.Location);
                            break;
                        }
                    default:
                        {
                            var leftExpr = (binExpr.Left as AstExpression);
                            var rightExpr = (binExpr.Right as AstExpression);

                            // special cringe case to compare structs
                            if (leftExpr.OutType.IsExactly<StructType>() && rightExpr.OutType.IsExactly<StructType>()
                                && (binExpr.Operator == "==" || binExpr.Operator == "!="))
                            {
                                // different structs are not the same
                                if ((leftExpr.OutType as StructType).Declaration != (rightExpr.OutType as StructType).Declaration)
                                {
                                    toReturn = GenerateExpressionCode(new AstBoolExpr(binExpr.Operator == "!="));
                                    break;
                                }

                                // special keys for struct comparisons
                                var left = GenerateExpressionCode(leftExpr, true); // we need ptrs
                                // return if the value was not properly generated
                                if (left == default)
                                    return default;
                                var right = GenerateExpressionCode(rightExpr, true); // we need ptrs
                                // return if the value was not properly generated
                                if (right == default)
                                    return default;

                                // TODO: mb better to not use memcmp because of padding bytes
                                // better to use comparison by fields?

                                // making memcmp
                                int structSize = leftExpr.OutType.GetSize();
                                var sizeLlvm = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.IntPtrTypeInstance), (ulong)structSize);
                                var compared = GetMemcmp(left, right, sizeLlvm);

                                // need to compare to 0 that they are equal
                                var fCmp = GetICompare(binExpr.Operator == "!=" ? LLVMIntPredicate.LLVMIntNE : LLVMIntPredicate.LLVMIntEQ);
                                var val = fCmp(_builder, compared, LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.BoolTypeInstance), (ulong)0), "binOp");
                                toReturn = val;
                                break;
                            }
                            // make special case for ptr diffs
                            else if (binExpr.Operator == "-" && leftExpr.OutType is PointerType ptrL && rightExpr.OutType is PointerType)
                            {
                                var left = GenerateExpressionCode(leftExpr);
                                // return if the value was not properly generated
                                if (left == default)
                                    return default;
                                var right = GenerateExpressionCode(rightExpr);
                                // return if the value was not properly generated
                                if (right == default)
                                    return default;

                                var leftV = CreateCast(_builder, left, leftExpr.OutType, HapetType.CurrentTypeContext.IntPtrTypeInstance);
                                var rightV = CreateCast(_builder, right, rightExpr.OutType, HapetType.CurrentTypeContext.IntPtrTypeInstance);

                                var res = _builder.BuildSub(leftV, rightV, "ptrDiff");
                                res = _builder.BuildUDiv(res, LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.IntPtrTypeInstance), (uint)ptrL.TargetType.GetSize()));
                                toReturn = CreateCast(_builder, res, HapetType.CurrentTypeContext.IntPtrTypeInstance, binExpr.OutType);
                                break;
                            }
                            else
                            {
                                var left = GenerateExpressionCode(leftExpr);
                                // return if the value was not properly generated
                                if (left == default)
                                    return default;
                                var right = GenerateExpressionCode(rightExpr);
                                // return if the value was not properly generated
                                if (right == default)
                                    return default;

                                var bo = GetBinOp(builtInOp);
                                var val = bo(_builder, left, right, "binOp");
                                toReturn = val;
                                break;
                            }
                        }
                }
            }
            else if (binExpr.ActualOperator is UserDefinedBinaryOperator userDef)
            {
                var leftExpr = (binExpr.Left as AstExpression);
                var left = GenerateExpressionCode(leftExpr);
                // return if the value was not properly generated
                if (left == default)
                    return default;
                var rightExpr = (binExpr.Right as AstExpression);
                var right = GenerateExpressionCode(rightExpr);
                // return if the value was not properly generated
                if (right == default)
                    return default;

                var fncType = _typeMap[userDef.Function];
                var fncValue = _valueMap[userDef.Function.Declaration.Symbol];

                toReturn = _builder.BuildCall2(fncType, fncValue, new LLVMValueRef[] { left, right }, "binOp");
            }

            if (toReturn != default)
            {
                if (getPtr)
                {
                    LLVMValueRef varPtr = CreateLocalVariable(binExpr.OutType, "binRetHolder");
                    _builder.BuildStore(toReturn, varPtr);
                    return varPtr;
                }
                return toReturn;
            }
            _messageHandler.ReportMessage(_currentSourceFile.Text, binExpr, [binExpr.ToString()], ErrorCode.Get(CTEN.StmtNotImplemented));
            return default;
        }
        
        private LLVMValueRef CheckIsCouldBeCasted(LLVMValueRef obj, HapetType objType, HapetType requiredType, bool isNot = false, ILocation castLocation = null)
        {
            if (objType is PointerType pt1 && pt1.TargetType is ClassType leftType)
            {
                ClassType rightType;
                if (requiredType is PointerType pt2 && pt2.TargetType is ClassType clsT)
                {
                    rightType = clsT;
                }
                else
                {
                    /// WARN: almost the same as in <see cref="CreateCast"/>
                    // check cast from object instance to struct

                    var castTypeInfo = _builder.BuildLoad2(GetTypeType(), _typeDictionary[requiredType], "typeLoaded");

                    // WARN: hard cock
                    var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("TypeConverter"));
                    DeclSymbol downcasterSymbol;
                    downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("CanBeDowncasted")) as DeclSymbol;
                    var downcasterFunc = _valueMap[downcasterSymbol];
                    LLVMTypeRef funcType = _typeMap[downcasterSymbol.Decl.Type.OutType];
                    var canBeDowncasted = _builder.BuildCall2(funcType, downcasterFunc, new LLVMValueRef[] { obj, castTypeInfo }, "canBeDowncasted");
                    return canBeDowncasted;
                }

                // you would ask - wtf is anyIsInterface? - read upper 

                bool anyIsInterface = rightType.Declaration.IsInterface || leftType.Declaration.IsInterface;
                bool isUpcast = leftType.IsInheritedFrom(rightType) || (leftType == rightType);
                // check upcast
                if (!isUpcast || anyIsInterface)
                {
                    // swap them and check inheritance
                    bool isDownCast = rightType.IsInheritedFrom(leftType);
                    if (isDownCast || anyIsInterface)
                    {
                        var castTypeInfo = _builder.BuildLoad2(GetTypeType(), _typeDictionary[rightType], "typeLoaded");

                        // WARN: hard cock
                        var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("TypeConverter"));
                        DeclSymbol downcasterSymbol;
                        if (rightType.Declaration.IsInterface)
                            downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("CanBeDowncastedInterface")) as DeclSymbol;
                        else
                            downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("CanBeDowncasted")) as DeclSymbol;
                        var downcasterFunc = _valueMap[downcasterSymbol];
                        LLVMTypeRef funcType = _typeMap[downcasterSymbol.Decl.Type.OutType];
                        var canBeDowncasted = _builder.BuildCall2(funcType, downcasterFunc, new LLVMValueRef[] { obj, castTypeInfo }, "canBeDowncasted");

                        // if 'is not' cringe - negate
                        if (isNot)
                            canBeDowncasted = _builder.BuildNot(canBeDowncasted, "negated");

                        return canBeDowncasted;
                    }
                    else
                    {
                        _messageHandler.ReportMessage(_currentSourceFile.Text, castLocation, [HapetType.AsString(leftType), HapetType.AsString(rightType)], ErrorCode.Get(CTEN.TypeCouldNotBeConverted));
                        return default;
                    }
                }
                else
                {
                    // just true when upcast shite
                    return GenerateExpressionCode(new AstBoolExpr(!isNot));
                }
            }
            else
            {
                // cringe shite like 
                // valueType is ISome

                var leftValueType = objType as StructType;
                if (requiredType is PointerType pt3 && pt3.TargetType is ClassType target)
                {
                    // just false if not inherited
                    if (leftValueType.IsInheritedFrom(target))
                        return GenerateExpressionCode(new AstBoolExpr(!isNot)); // true
                    return GenerateExpressionCode(new AstBoolExpr(isNot));
                }
                else if (requiredType == leftValueType)
                {
                    // just true - it is like
                    // valueType is TheSameType
                    return GenerateExpressionCode(new AstBoolExpr(!isNot));
                }
                else
                {
                    // TODO: error - user tried to do shite like
                    // valueType is AnotherValueType
                    return default;
                }
            }
        }

        private LLVMValueRef GeneratePointerExprCode(AstPointerExpr expr, bool getPtr = false)
        {
            if (expr.IsDereference)
            {
                var theVar = GenerateExpressionCode(expr.SubExpression, getPtr);
                var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(getPtr ? expr.SubExpression.OutType : expr.OutType), theVar, $"derefed");
                return loaded;
            }
            else
            {
                // idk what to do here :_(
                // anyway it should not happen...
                // internal error here
                _messageHandler.ReportMessage(_currentSourceFile.Text, expr, [], ErrorCode.Get(CTEN.PtrExprCouldNotBeGenerated));
            }
            return default;
        }

        private LLVMValueRef GenerateAddressOfExprCode(AstAddressOfExpr addrExpr)
        {
            // WARN: should be better. probably there won't be only AstNestedExpr or AstIdExpr but something else...
            if (addrExpr.SubExpression is AstNestedExpr nestExpr)
            {
                return GenerateNestedExpr(nestExpr, true);
            }
            else if (addrExpr.SubExpression is AstIdExpr idExpr)
            {
                return GenerateIdExpr(idExpr, true);
            }
            // internal error here
            _messageHandler.ReportMessage(_currentSourceFile.Text, addrExpr, [], ErrorCode.Get(CTEN.AddrOfExprCouldNotBeGenerated));
            return default;
        }

        private unsafe LLVMValueRef GenerateIdExpr(AstIdExpr expr, bool getPtr = false)
        {
            LLVMValueRef v = default;
            // check that the symbol is a declaration
            if (expr.FindSymbol is not DeclSymbol declSymbol)
                return v;
            var theDecl = declSymbol.Decl;

            // return default because user tries to access enum field - no need to do anything here
            if (theDecl is AstEnumDecl)
                return v;

            // check if it const/static shite
            if (theDecl is AstVarDecl varDecl && (theDecl.SpecialKeys.Contains(TokenType.KwStatic) || theDecl.SpecialKeys.Contains(TokenType.KwConst)))
            {
                if (varDecl.ContainingParent is not AstClassDecl && varDecl.ContainingParent is not AstStructDecl)
                    return v;
                // we need to get original var decl
                if (varDecl.ContainingParent.IsImplOfGeneric)
                    declSymbol = varDecl.ContainingParent.OriginalGenericDecl.GetDeclarations().GetSameDeclByTypeAndNamePure(varDecl, out int _).Symbol as DeclSymbol;
                
                if (!_valueMap.TryGetValue(declSymbol, out v))
                    return default;

                if (getPtr)
                    return v;

                // need to load it as a ptr
                var varType = expr.OutType;

                var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(varType), v, expr.Name);
                return loaded;
            }
            // this check is done to generate proper delegate
            else if (expr.OutType is FunctionType fncType && theDecl is AstFuncDecl)
            {
                // by default it is a nullptr
                LLVMValueRef ptrToObject = LLVM.ConstPointerNull(HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(1, false)));
                return CreateDelegateFromFunction(fncType, declSymbol, ptrToObject);
            }
            else
            {
                if (!_valueMap.TryGetValue(declSymbol, out v))
                    return default;

                // for ref and out we need to load a pointer
                bool isRefOrOut = theDecl is AstParamDecl pD && (pD.ParameterModificator == ParameterModificator.Ref || pD.ParameterModificator == ParameterModificator.Out);
                if (isRefOrOut)
                    v = _builder.BuildLoad2(HapetTypeToLLVMType(HapetType.CurrentTypeContext.PtrToVoidType), v, $"{expr.Name}_loaded_ref");

                // return the ptr to the val. used for AstAddressOf or storing values
                if (getPtr)
                    return v;

                // need to load it as a ptr
                var varType = expr.OutType;

                var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(varType), v, expr.Name);
                return loaded;
            }
        }

        private unsafe LLVMValueRef GenerateNewExpr(AstNewExpr expr, bool getPtr = false)
        {
            LLVMValueRef v = default;
            if (expr.OutType is PointerType pt && pt.TargetType is ClassType classType)
            {
                int structSize = classType.GetSize();

                // getting class ctor
                var ctorSymbol = expr.ConstructorSymbol;

                // allocating memory for struct
                // check for unsafe
                if (expr.IsUnsafeNew)
                    v = GetMalloc(structSize, 1);
                else
                    v = GetNewClassInstance(structSize, _typeDictionary[classType]);
                // making offset
                // WARN: always 8 offset is here
                var normalOffset = LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)1);
                // get ptr by offset
                v = _builder.BuildGEP2(_context.Int64Type, v, new LLVMValueRef[] { normalOffset }, "offsetedV");

                // set up type data ptr!!!
                SetTypeInfo(v, classType);

                // other args
                List<LLVMValueRef> args = new List<LLVMValueRef>() { v };
                // skip the first object param
                var pars = (ctorSymbol.Decl as AstFuncDecl).Parameters.Skip(1).ToList();
                List<AstArgumentExpr> normalArgs = _postPreparer.GenerateNormalArguments(pars, expr.Arguments, expr);
                foreach (var a in normalArgs)
                {
                    args.Add(GenerateExpressionCode(a));
                }

                var ctorFunc = _valueMap[ctorSymbol];
                LLVMTypeRef ctorType = _typeMap[ctorSymbol.Decl.Type.OutType];
                _builder.BuildCall2(ctorType, ctorFunc, args.ToArray());  // calling ctor

                if (getPtr)
                {
                    LLVMValueRef varPtr = CreateLocalVariable(expr.OutType, "newRetHolder");
                    _builder.BuildStore(v, varPtr);
                    return varPtr;
                }
                return v;
            }
            else if (expr.OutType is StructType structType)
            {
                // getting struct ctor
                var ctorSymbol = expr.ConstructorSymbol;
                v = _builder.BuildAlloca(HapetTypeToLLVMType(structType), $"var_{structType.Declaration.Name.Name}");

                // other args
                List<LLVMValueRef> args = new List<LLVMValueRef>() { v };
                // skip the first object param
                var pars = (ctorSymbol.Decl as AstFuncDecl).Parameters.Skip(1).ToList();
                List<AstArgumentExpr> normalArgs = _postPreparer.GenerateNormalArguments(pars, expr.Arguments, expr);
                foreach (var a in normalArgs)
                {
                    args.Add(GenerateExpressionCode(a));
                }

                var ctorFunc = _valueMap[ctorSymbol];
                LLVMTypeRef ctorType = _typeMap[ctorSymbol.Decl.Type.OutType];
                _builder.BuildCall2(ctorType, ctorFunc, args.ToArray());  // calling ctor

                if (getPtr)
                    return v;
                return _builder.BuildLoad2(HapetTypeToLLVMType(structType), v, "ctoredLoaded");
            }

            return v;
        }

        private unsafe LLVMValueRef GenerateCallExpr(AstCallExpr expr, bool getPtr = false, List<LLVMValueRef> additionalArgs = null)
        {
            // the func is needed to handle virtual shite
            LLVMValueRef CreateCall(LLVMBuilderRef builder, LLVMTypeRef funcType, FunctionType hapetType, LLVMValueRef func, List<LLVMValueRef> args, bool isBaseCall, HapetType objectType, string name = "")
            {
                // when calling funcs of struct - call directly
                // except functions of ValueType and Object
                if (hapetType.Declaration.ContainingParent is not AstClassDecl clsDecl)
                {
                    return builder.BuildCall2(funcType, func, args.ToArray(), name);
                }
                if (objectType is StructType strT)
                {
                    // need to box this shite 
                    var structLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(strT), args[0], "strLoaded");
                    args[0] = CreateCast(_builder, structLoaded, strT, PointerType.GetPointerType(clsDecl.Type.OutType));
                }

                // for base func call - just call base func :)
                if (isBaseCall)
                    return builder.BuildCall2(funcType, func, args.ToArray(), name);

                var virtualMethod = clsDecl.AllVirtualMethods.GetSameByNameAndTypes(hapetType.Declaration, out int index);
                // if it is a virtual method call
                if (virtualMethod != null)
                {
                    if (clsDecl.IsInterface)
                    {
                        // WARN: hard cock
                        var helper = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("VtableHelper"));
                        var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("GetInterfaceMethodByIndex")) as DeclSymbol;
                        var methFunc = _valueMap[methSymbol];
                        LLVMTypeRef methType = _typeMap[methSymbol.Decl.Type.OutType];
                        var castTypeInfo = _builder.BuildLoad2(GetTypeType(), _typeDictionary[clsDecl.Type.OutType], "typeLoaded");
                        var funcToCall = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { args[0], castTypeInfo, LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)index) }, "funcToCall");
                        return builder.BuildCall2(funcType, funcToCall, args.ToArray(), name);
                    }
                    else
                    {
                        // WARN: hard cock
                        var helper = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("VtableHelper"));
                        var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("GetMethodByIndex")) as DeclSymbol;
                        var methFunc = _valueMap[methSymbol];
                        LLVMTypeRef methType = _typeMap[methSymbol.Decl.Type.OutType];
                        var funcToCall = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { args[0], LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)index) }, "funcToCall");
                        return builder.BuildCall2(funcType, funcToCall, args.ToArray(), name);
                    }
                }
                return builder.BuildCall2(funcType, func, args.ToArray(), name);
            }

            // creating a variable to store function result. for what?
            // because in some places in code generation we need for var pointer
            // but if we do not allocate any var - so how would we get the ptr?
            // to solve the problem we implicitly create a varialbe that would contain return value
            // so 'Anime().Length;' -> 'var a = Anime(); a.Length;'
            // WARN! create the var only if the func has non void ret type!!!

            if (expr.FuncName.OutType is FunctionType fncType)
            {
                var hapetFunc = _valueMap[fncType.Declaration.Symbol];
                LLVMTypeRef funcType = _typeMap[fncType];

                LLVMValueRef varPtr = default;
                if (expr.OutType is not VoidType)
                    varPtr = CreateLocalVariable(expr.OutType, "funcRetHolder");

                HapetType objectType = null;
                // args shite
                List<LLVMValueRef> args = new List<LLVMValueRef>();
                if (!expr.StaticCall)
                {
                    // we need to get ptr of var when calling struct func
                    if (expr.TypeOrObjectName.OutType is StructType)
                        args.Add(GenerateExpressionCode(expr.TypeOrObjectName, true));
                    else
                        args.Add(GenerateExpressionCode(expr.TypeOrObjectName));
                    objectType = expr.TypeOrObjectName.OutType;
                }

                // this is a super kostyl to handle 
                // anime.Buffer++
                // so sometimes we don't want to gen normal args
                // we just want to add our own
                if (additionalArgs == null)
                {
                    // skip the first object param
                    var parsToSearch = expr.StaticCall ? fncType.Declaration.Parameters : fncType.Declaration.Parameters.Skip(1).ToList();
                    List<AstArgumentExpr> normalArgs = _postPreparer.GenerateNormalArguments(parsToSearch, expr.Arguments, expr);
                    foreach (var a in normalArgs)
                    {
                        args.Add(GenerateExpressionCode(a));
                    }
                }
                else
                {
                    args.AddRange(additionalArgs);
                }

                // is that smth like 'base.Anime()' call
                // TODO: won't work with smth like '(base as AnimeCls).AnimeFunc()'
                bool isBaseCall = false;
                if (expr.TypeOrObjectName is AstNestedExpr nest)
                    isBaseCall = nest.RightPart is AstIdExpr id && id.Name == "base";
                else if (expr.TypeOrObjectName is AstIdExpr id)
                    isBaseCall = id.Name == "base";

                // the return name has to be empty if ret value of func is void
                // also save the ret value into a var
                if (expr.OutType is not VoidType)
                {
                    // save the value
                    LLVMValueRef ret = CreateCall(_builder, funcType, fncType, hapetFunc, args, isBaseCall, objectType, $"funcReturnValue");
                    _builder.BuildStore(ret, varPtr);

                    if (getPtr)
                        return varPtr;

                    // need to load it as a ptr
                    var retType = expr.OutType;

                    return _builder.BuildLoad2(HapetTypeToLLVMType(retType), varPtr, "holderLoaded");
                }

                return CreateCall(_builder, funcType, fncType, hapetFunc, args, isBaseCall, objectType);
            }
            else if (expr.FuncName.OutType is DelegateType delType)
            {
                var hapetDelegate = GenerateIdExpr(expr.FuncName, true);
                LLVMTypeRef delegateType = GetDelegateAnonType(delType);

                LLVMValueRef varPtr = default;
                if (delType.TargetDeclaration.Returns.OutType is not VoidType)
                    varPtr = CreateLocalVariable(delType.TargetDeclaration.Returns.OutType, "delRetHolder");

                // args shite
                List<LLVMValueRef> args = new List<LLVMValueRef>();
                // skip the first object param
                var parsToSearch = delType.TargetDeclaration.Parameters;
                List<AstArgumentExpr> normalArgs = _postPreparer.GenerateNormalArguments(parsToSearch, expr.Arguments, expr);
                foreach (var a in normalArgs)
                {
                    args.Add(GenerateExpressionCode(a));
                }

                var loadedDelegate = _builder.BuildLoad2(delegateType, hapetDelegate, $"delegateLoaded");
                var theRealFuncExtracted = _builder.BuildExtractValue(loadedDelegate, 0, "funcExtracted");
                var ptrToObject = _builder.BuildExtractValue(loadedDelegate, 1, "ptrToObject");

                // creating other blocks
                var bbTrue = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"del.null");
                var bbFalse = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"del.notnull");
                var bbEnd = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"del.end");

                var nullPtrT = PointerType.GetPointerType(HapetType.CurrentTypeContext.GetIntType(1, false));
                var nullPtr = LLVM.ConstPointerNull(HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(1, false)));
                var leftV = CreateCast(_builder, nullPtr, nullPtrT, HapetType.CurrentTypeContext.IntPtrTypeInstance);
                var rightV = CreateCast(_builder, ptrToObject, nullPtrT, HapetType.CurrentTypeContext.IntPtrTypeInstance);
                var binOp = SearchBinOp("==", HapetType.CurrentTypeContext.IntPtrTypeInstance, HapetType.CurrentTypeContext.IntPtrTypeInstance);
                var res = binOp(_builder, leftV, rightV, "cmpResult");
                _builder.BuildCondBr(res, bbTrue, bbFalse);

                // if obj is null
                _builder.PositionAtEnd(bbTrue);

                // getting the function type to call
                var funcType = GetFunctionTypeOfDelegate(delType);
                // the return name has to be empty if ret value of func is void
                // also save the ret value into a var
                if (delType.TargetDeclaration.Returns.OutType is not VoidType)
                {
                    LLVMValueRef ret = _builder.BuildCall2(funcType, theRealFuncExtracted, args.ToArray(), $"delReturnValue");
                    _builder.BuildStore(ret, varPtr);
                }
                else
                {
                    _builder.BuildCall2(funcType, theRealFuncExtracted, args.ToArray());
                }
                _builder.BuildBr(bbEnd);

                // if obj is not null
                _builder.PositionAtEnd(bbFalse);
                funcType = GetFunctionTypeOfDelegate(delType, false);
                args.Insert(0, ptrToObject);
                // the return name has to be empty if ret value of func is void
                // also save the ret value into a var
                if (delType.TargetDeclaration.Returns.OutType is not VoidType)
                {
                    LLVMValueRef ret = _builder.BuildCall2(funcType, theRealFuncExtracted, args.ToArray(), $"delReturnValue");
                    _builder.BuildStore(ret, varPtr);
                }
                else
                {
                    _builder.BuildCall2(funcType, theRealFuncExtracted, args.ToArray());
                }
                _builder.BuildBr(bbEnd);

                _builder.PositionAtEnd(bbEnd);

                if (delType.TargetDeclaration.Returns.OutType is not VoidType)
                {
                    if (getPtr)
                        return varPtr;
                    return _builder.BuildLoad2(HapetTypeToLLVMType(delType.TargetDeclaration.Returns.OutType), varPtr, "holderLoaded");
                }
                else
                    return default;
            }
            else
            {
                // handle special call
                if (expr.IsSpecialExternalCall)
                {
                    // need to declare it at first
                    var funcType = LLVMTypeRef.CreateFunction(_context.VoidType, [], false);
                    // declaring external global func
                    var funcValue = _module.AddFunction($"{expr.FuncName.Name}()", funcType);
                    funcValue.Linkage = LLVMLinkage.LLVMExternalLinkage;
                    funcValue.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;

                    return _builder.BuildCall2(funcType, funcValue, []);
                }
                _messageHandler.ReportMessage(_currentSourceFile.Text, expr, [HapetType.AsString(expr.FuncName.OutType)], ErrorCode.Get(CTEN.TheTypeIsNotCallable));
                return default;
            }
        }

        private unsafe LLVMValueRef GenerateArgumentExpr(AstArgumentExpr expr)
        {
            // for ref and out we need to take a pointer
            bool isRefOrOut = expr.ArgumentModificator == ParameterModificator.Ref || expr.ArgumentModificator == ParameterModificator.Out;

            return GenerateExpressionCode(expr.Expr, isRefOrOut);
        }

        private unsafe LLVMValueRef GenerateCastExpr(AstCastExpr expr, bool getPtr = false)
        {
            var sub = GenerateExpressionCode(expr.SubExpression, false);
            var val = CreateCast(_builder, sub, expr.SubExpression.OutType, expr.OutType);
            if (getPtr)
            {
                LLVMValueRef varPtr = CreateLocalVariable(expr.OutType, "castHolder");
                _builder.BuildStore(val, varPtr);
                return varPtr;
            }
            return val;
        }

        private unsafe LLVMValueRef GenerateNestedExpr(AstNestedExpr expr, bool getPtr = false)
        {
            if (expr.LeftPart == null)
            {
                // func call, ident or pure expr
                return GenerateExpressionCode(expr.RightPart, getPtr);
            }
            else
            {
                // checked in PP
                var idExpr = expr.RightPart as AstIdExpr;

                // we need to get 'struct' elements by ref to access it's elements
                bool getByRef = (expr.LeftPart.OutType is StructType);
                var leftPart = GenerateExpressionCode(expr.LeftPart, getByRef);

                // getting struct/class/interface declarations and the type
                HapetType leftPartType = null;
                AstDeclaration leftPartDecl = null;
                List<AstDeclaration> leftPartDeclarations = null;
                // this is usually when accesing static/const values
                // like 'Attribute.CoonstField'
                if (expr.LeftPart.OutType is ClassType classTT)
                {
                    leftPartDecl = classTT.Declaration;
                    leftPartDeclarations = classTT.Declaration.Declarations.Where(x => x is AstVarDecl).ToList();
                    leftPartType = classTT;
                }
                else if (expr.LeftPart.OutType is PointerType pt && pt.TargetType is ClassType classT)
                {
                    leftPartDecl = classT.Declaration;
                    leftPartDeclarations = classT.Declaration.Declarations.Where(x => x is AstVarDecl).ToList();
                    leftPartType = classT;
                }
                else if (expr.LeftPart.OutType is StructType structT)
                {
                    leftPartDecl = structT.Declaration;
                    leftPartDeclarations = structT.Declaration.Declarations;
                    leftPartType = structT;
                }
                else if (expr.LeftPart.OutType is EnumType enumT)
                {
                    leftPartDecl = enumT.Declaration;
                    leftPartDeclarations = enumT.Declaration.Declarations.Select(x => x as AstDeclaration).ToList();
                    leftPartType = enumT;
                }

                // getting index of the element and the element itself
                if (leftPartDeclarations != null && leftPartType != null && leftPartDecl != null)
                {
                    // this is different for enums
                    if (leftPartType is EnumType enumT)
                    {
                        var enumFieldName = $"{enumT}::{idExpr.Name}";
                        var v = _module.GetNamedGlobal(enumFieldName);
                        if (getPtr)
                            return v;
                        var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType), v, $"{idExpr.Name}Loaded");
                        return loaded;
                    }
                    // check if the field is static/const
                    else if (IsStaticOrConstElement(idExpr.Name, leftPartDeclarations, out AstVarDecl theDecl))
                    {
                        // static/const elements are accessed in different way
                        if (theDecl.ContainingParent is not AstClassDecl && theDecl.ContainingParent is not AstStructDecl)
                            return default;
                        LLVMValueRef v = default;
                        if (!_valueMap.TryGetValue(theDecl.Name.FindSymbol, out v))
                            return default;

                        if (getPtr)
                            return v;

                        // need to load it as a ptr
                        var varType = expr.OutType;

                        var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(varType), v, $"{idExpr.Name}Loaded");
                        return loaded;
                    }
                    else
                    {
                        // check if getting function
                        if (idExpr.OutType is FunctionType fncT)
                        {
                            // by default it is a nullptr
                            LLVMValueRef ptrToObject;
                            if (fncT.IsStaticFunction())
                                ptrToObject = LLVM.ConstPointerNull(HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(1, false)));
                            else
                                ptrToObject = leftPart;
                            return CreateDelegateFromFunction(fncT, idExpr.FindSymbol as DeclSymbol, ptrToObject);
                        }

                        // usually this happens when user tries to access non static/const field from a class/struct name
                        if (leftPart == default)
                        {
                            _messageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [idExpr.Name], ErrorCode.Get(CTEN.NonStaticFieldAccess));
                            return default;
                        }

                        // getting the index of the element
                        uint elementIndex = GetElementIndex(idExpr.Name, leftPartDecl);
                        Debug.Assert(elementIndex != uint.MaxValue);

                        // getting normal element index when user used custom struct alignment
                        if (leftPartType is StructType strT && strT.IsUserDefinedAlignment)
                            elementIndex = _structOffsets[strT][elementIndex];

                        var tp = HapetTypeToLLVMType(leftPartType, true);
                        LLVMValueRef ret;

                        // another way of accessing elements when using interfaces
                        if (leftPartType is ClassType clsT && clsT.Declaration.IsInterface)
                        {
                            var castTypeInfo = _builder.BuildLoad2(GetTypeType(), _typeDictionary[clsT], "typeLoaded");
                            var elementIndexValueRef = LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)elementIndex);
                            // WARN: hard cock
                            var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("TypeConverter"));
                            var offseterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("System.Runtime.Conversion.TypeConverter::GetInterfaceOffset(void*:System.Runtime.TypeInfoUnsafe*:int)")) as DeclSymbol;
                            var offseterFunc = _valueMap[offseterSymbol];
                            LLVMTypeRef funcType = _typeMap[offseterSymbol.Decl.Type.OutType];
                            var offset = _builder.BuildCall2(funcType, offseterFunc, new LLVMValueRef[] { leftPart, castTypeInfo, elementIndexValueRef }, "interfaceOffset");

                            // get ptr by offset
                            ret = _builder.BuildGEP2(_context.Int8Type, leftPart, new LLVMValueRef[] { offset }, idExpr.Name);
                        }
                        else
                        {
                            ret = _builder.BuildStructGEP2(tp, leftPart, elementIndex, idExpr.Name);
                        }

                        // if we need ptr for the shite. usually used to store some values inside vars
                        if (getPtr)
                            return ret;

                        // need to load it as a ptr
                        var varType = idExpr.OutType;

                        // loading the field because it is not registered in _typeMap like a normal variable.
                        // it should be ok for all types of the fields including classes and other shite
                        var retLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(varType), ret, $"{idExpr.Name}Loaded");
                        return retLoaded;
                    }
                }
            }
            _messageHandler.ReportMessage(_currentSourceFile.Text, expr, [], ErrorCode.Get(CTEN.NestedCouldNotBeGenerated));
            return default;
        }

        private static bool IsStaticOrConstElement(string name, List<AstDeclaration> decls, out AstVarDecl decl)
        {
            // getting pure decls with consts and statics
            var pureDecls = decls.Where(x => x.SpecialKeys.Contains(TokenType.KwStatic) || x.SpecialKeys.Contains(TokenType.KwConst)).ToList();
            decl = pureDecls.FirstOrDefault(x => x.Name.Name == name) as AstVarDecl;
            return decl != null;
        }

        private static uint GetElementIndex(string name, AstDeclaration parentDecl)
        {
            AstDeclaration lastFound = null;
            uint lastFoundIndex = uint.MaxValue;

            // getting pure decls without consts and statics
            var pureDecls = parentDecl.GetAllRawFields();
            // search for the name in decl
            for (uint i = 0; i < pureDecls.Count; ++i)
            {
                var decl = pureDecls[(int)i];
                if (decl.Name.Name == name)
                {
                    // if we found an override :)
                    if (lastFound != null && decl.SpecialKeys.Contains(TokenType.KwNew))
                    {
                        lastFound = decl;
                        lastFoundIndex = i;
                        continue;
                    }
                    else if (lastFound != null)
                        continue; // also continue - probably no need to shadow - probably an error :)

                    lastFound = decl;
                    lastFoundIndex = i; // getting the field index
                }
            }
            return lastFoundIndex;
        }

        private LLVMValueRef GenerateArrayCreateExprCode(AstArrayCreateExpr expr, bool getPtr = false)
        {
            // TODO: check if it could be allocated on stack

            var cloned = expr.Clone() as AstArrayCreateExpr;
            return GenerateArrayInternal(cloned, getPtr);
        }

        private LLVMValueRef GenerateArrayAccessExprCode(AstArrayAccessExpr expr, bool getPtr = false)
        {
            // the buffer to be indexed
            LLVMValueRef buffer = default;

            if (expr.ObjectName.OutType is PointerType)
            {
                // getting pointer to the var
                LLVMValueRef ptrToPtr = GenerateExpressionCode(expr.ObjectName, true);
                buffer = _builder.BuildLoad2(HapetTypeToLLVMType(expr.ObjectName.OutType), ptrToPtr);
            }

            // if the gotten buffer is not null
            if (buffer != default)
            {
                // getting an element from the arrayBuf
                LLVMValueRef llvmElementIndex = GenerateExpressionCode(expr.ParameterExpr);
                var arrayEl = _builder.BuildGEP2(HapetTypeToLLVMType(expr.OutType), buffer, new LLVMValueRef[] { llvmElementIndex });

                if (getPtr)
                    return arrayEl;

                // need to load it as a ptr
                var varType = expr.OutType;

                var retLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(varType), arrayEl);
                return retLoaded;
            }

            _messageHandler.ReportMessage(_currentSourceFile.Text, expr, [HapetType.AsString(expr.ObjectName.OutType)], ErrorCode.Get(CTEN.ArrayAccessNotGenerate));
            return default;
        }

        private unsafe LLVMValueRef GenerateTernaryExprCode(AstTernaryExpr expr)
        {
            // WARN: almost the same as AstIfStmt!!!
            bool needVariable = expr.OutType is not VoidType;

            var bbBody = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"tern.body");

            // creating other blocks
            var bbElse = _context.CreateBasicBlock($"tern.else");
            var bbEnd = _context.CreateBasicBlock($"tern.end");

            // tmp var
            LLVMValueRef varPtr = default; 
            if (needVariable)
                varPtr = CreateLocalVariable(expr.OutType, "tmpTernVar");

            // building the condition
            var cmp = GenerateExpressionCode(expr.Condition);
            _builder.BuildCondBr(cmp, bbBody, bbElse);

            // body
            _builder.PositionAtEnd(bbBody);
            var r1 = GenerateExpressionCode(expr.TrueExpr);
            if (needVariable)
                AssignToVar(varPtr, r1);
            _builder.BuildBr(bbEnd);

            // else
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbElse);
            _builder.PositionAtEnd(bbElse);
            // generating else code
            var r2 = GenerateExpressionCode(expr.FalseExpr);
            if (needVariable)
                AssignToVar(varPtr, r2);
            _builder.BuildBr(bbEnd);

            // appending them sooner
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);
            _builder.PositionAtEnd(bbEnd);

            if (needVariable)
            {
                // need to make a ptr to a class
                var resultType = expr.OutType;
                return _builder.BuildLoad2(HapetTypeToLLVMType(resultType), varPtr, "ternLoaded");
            }
            return default;
        }

        private unsafe LLVMValueRef GenerateCheckedExprCode(AstCheckedExpr expr)
        {
            // TODO: 
            return GenerateExpressionCode(expr.SubExpression);
        }

        private unsafe LLVMValueRef GenerateSATExprCode(AstSATOfExpr expr, bool getPtr = false)
        {
            switch (expr.ExprType)
            {
                case TokenType.KwSizeof:
                    return HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetIntType(4, true), expr.TargetType.OutType.GetSize(), getPtr);
                case TokenType.KwAlignof:
                    return HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetIntType(4, true), expr.TargetType.OutType.GetAlignment(), getPtr);
                case TokenType.KwNameof:
                    return HapetValueToLLVMValue(HapetType.CurrentTypeContext.StringTypeInstance, expr.TargetType.TryFlatten(null, null), getPtr);
                case TokenType.KwTypeof:
                    if (getPtr) return _typeDictionary[expr.TargetType.OutType];
                    else return _builder.BuildLoad2(GetTypeType(), _typeDictionary[expr.TargetType.OutType], "typeLoaded");
            }
            throw new InvalidDataException();
        }

        private unsafe LLVMValueRef GenerateEmptyStructExprCode(AstEmptyStructExpr expr)
        {
            // getting types
            var structType = expr.TypeForDefault;
            int structSize = structType.GetSize();
            var allocated = _builder.BuildAlloca(HapetTypeToLLVMType(structType), "allocatedEmpty");

            // making consts
            var zeroLlvm = LLVMValueRef.CreateConstInt(_context.Int32Type, 0);
            var sizeLlvm = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.IntPtrTypeInstance), (ulong)structSize);

            // memset
            var marshalDecl = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("Marshal"));
            var memsetSymbol = (marshalDecl.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Memset")) as DeclSymbol;
            var memsetFunc = _valueMap[memsetSymbol];
            LLVMTypeRef funcType = _typeMap[memsetSymbol.Decl.Type.OutType];
            _builder.BuildCall2(funcType, memsetFunc, new LLVMValueRef[] { allocated, zeroLlvm, sizeLlvm }, "zeroedEmpty");

            var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(structType), allocated, "loadedEmpty");
            return loaded;
        }

        private unsafe LLVMValueRef GenerateLambdaExprCode(AstLambdaExpr expr)
        {
            // by default it is a nullptr
            LLVMValueRef ptrToObject = LLVM.ConstPointerNull(HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(1, false)));
            return CreateDelegateFromLambda(expr.OutType as LambdaType, ptrToObject);
        }

        private unsafe LLVMValueRef GenerateNullExprCode(AstNullExpr expr)
        {
            return LLVM.ConstPointerNull(HapetTypeToLLVMType(expr.OutType));
        }

        // statements
        private void GenerateAssignStmt(AstAssignStmt stmt)
        {
            LLVMValueRef theVar = GenerateNestedExpr(stmt.Target, true);

            AssignToVar(theVar, stmt.Value);

            // TODO: WARN: Assign is a stmt and does not returns anything. could be changed to expr
            // so stmts like 'a = (b = 3);' would be allowed...
        }

        private ulong _forCounter;
        // these blocks are needed for break and continue statements
        private LLVMBasicBlockRef _currentLoopInc = null;
        private LLVMBasicBlockRef _currentLoopEnd = null;
        private AstStatement _currentLoop = null;
        private unsafe void GenerateForStmt(AstForStmt stmt)
        {
            // WARN: this strange code is not just for 'fun'
            // when creating nested 'for' loops it would be easier to read LLVM IR code with that shite
            // so for example if we have two nested 'for' loops it would look like this:
            // for1.cond: ...
            // for1.body: ...
            //   for2.cond: ...
            //   for2.body: ...
            //   for2.inc: ...
            //   for2.end: ...
            // for1.inc: ...
            // for1.end: ...

            _forCounter++;

            // saving previous blocks because of nesting
            var prevForInc = _currentLoopInc;
            var prevForEnd = _currentLoopEnd;
            var prevLoop = _currentLoop;

            if (stmt.FirstArgument != null)
                GenerateExpressionCode(stmt.FirstArgument);

            var bbCond = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"for{_forCounter}.cond");
            var bbBody = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"for{_forCounter}.body");

            // creating other blocks
            var bbInc = _context.CreateBasicBlock($"for{_forCounter}.inc");
            var bbEnd = _context.CreateBasicBlock($"for{_forCounter}.end");

            // directly br into loop condition
            _builder.BuildBr(bbCond);

            _currentLoopInc = bbInc;
            _currentLoopEnd = bbEnd;
            _currentLoop = stmt;

            // condition
            _builder.PositionAtEnd(bbCond);
            if (stmt.SecondArgument != null)
            {
                // building the condition
                var cmp = GenerateExpressionCode(stmt.SecondArgument);
                _builder.BuildCondBr(cmp, bbBody, bbEnd);
            }
            else
            {
                // if the second param is null - just move to the body block
                _builder.BuildBr(bbBody);
            }

            // body
            _builder.PositionAtEnd(bbBody);
            if (stmt.Body != null)
            {
                // generating body code
                GenerateExpressionCode(stmt.Body);
            }

            // appending them sooner
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbInc);
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);

            // check if it has br/ret 
            if (!AstBlockExpr.IsBlockHasItsOwnBr(stmt.Body))
            {
                // setting br without condition into inc block from body block
                _builder.BuildBr(bbInc);
            }

            // inc
            _builder.PositionAtEnd(bbInc);
            if (stmt.ThirdArgument != null)
            {
                // generating inc code
                GenerateExpressionCode(stmt.ThirdArgument);
            }
            _builder.BuildBr(bbCond);
            _builder.PositionAtEnd(bbEnd);

            // restoring prev blocks
            _currentLoopInc = prevForInc;
            _currentLoopEnd = prevForEnd;
            _currentLoop = prevLoop;
        }

        private ulong _whileCounter;
        private unsafe void GenerateWhileStmt(AstWhileStmt stmt)
        {
            // WARN: this strange code is not just for 'fun'
            // when creating nested 'while' loops it would be easier to read LLVM IR code with that shite
            // so for example if we have two nested 'while' loops it would look like this:
            // while1.cond: ...
            // while1.body: ...
            //   while2.cond: ...
            //   while2.body: ...
            //   while2.end: ...
            // while1.end: ...

            _whileCounter++;

            // saving previous blocks because of nesting
            // WARN: for 'while' loops there are no Inc block
            // so the Cond block is used directly
            var prevWhileInc = _currentLoopInc;
            var prevWhileEnd = _currentLoopEnd;
            var prevLoop = _currentLoop;

            var bbCond = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"while{_whileCounter}.cond");
            var bbBody = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"while{_whileCounter}.body");

            // creating other blocks
            var bbEnd = _context.CreateBasicBlock($"while{_whileCounter}.end");

            // directly br into loop condition
            _builder.BuildBr(bbCond);

            _currentLoopInc = bbCond; // check upper WARN
            _currentLoopEnd = bbEnd;
            _currentLoop = stmt;

            // condition
            _builder.PositionAtEnd(bbCond);
            if (stmt.Condition != null)
            {
                // building the condition
                var cmp = GenerateExpressionCode(stmt.Condition);
                _builder.BuildCondBr(cmp, bbBody, bbEnd);
            }
            else
            {
                // if the second param is null (should not happen!!! - checked in Parsing) - just move to the body block
                _builder.BuildBr(bbBody);
            }

            // body
            _builder.PositionAtEnd(bbBody);
            if (stmt.Body != null)
            {
                // generating body code
                GenerateExpressionCode(stmt.Body);
            }

            // check if it has br/ret 
            if (!AstBlockExpr.IsBlockHasItsOwnBr(stmt.Body))
            {
                // setting br without condition into inc block from body block
                _builder.BuildBr(bbCond);
            }

            // appending them sooner
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);

            _builder.PositionAtEnd(bbEnd);

            // restoring prev blocks
            _currentLoopInc = prevWhileInc;
            _currentLoopEnd = prevWhileEnd;
            _currentLoop = prevLoop;
        }

        private ulong _doWhileCounter;
        private unsafe void GenerateDoWhileStmt(AstDoWhileStmt stmt)
        {
            // WARN: this strange code is not just for 'fun'
            // when creating nested 'do-while' loops it would be easier to read LLVM IR code with that shite
            // so for example if we have two nested 'do-while' loops it would look like this:
            // while1.body: ...
            //   while2.body: ...
            //   while2.cond: ...
            //   while2.end: ...
            // while1.cond: ...
            // while1.end: ...

            _doWhileCounter++;

            // saving previous blocks because of nesting
            // WARN: for 'do-while' loops there are no Inc block
            // so the Cond block is used directly
            var prevWhileInc = _currentLoopInc;
            var prevWhileEnd = _currentLoopEnd;
            var prevLoop = _currentLoop;

            var bbBody = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"while{_whileCounter}.body");

            // creating other blocks
            var bbCond = _context.CreateBasicBlock($"while{_whileCounter}.cond");
            var bbEnd = _context.CreateBasicBlock($"while{_whileCounter}.end");

            // directly br into loop body
            _builder.BuildBr(bbBody);

            _currentLoopInc = bbCond; // check upper WARN
            _currentLoopEnd = bbEnd;
            _currentLoop = stmt;

            // body
            _builder.PositionAtEnd(bbBody);
            if (stmt.Body != null)
            {
                // generating body code
                GenerateExpressionCode(stmt.Body);
            }

            // check if it has br/ret 
            if (!AstBlockExpr.IsBlockHasItsOwnBr(stmt.Body))
            {
                // setting br without condition into inc block from body block
                _builder.BuildBr(bbCond);
            }

            // appending them sooner
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbCond);
            // condition
            _builder.PositionAtEnd(bbCond);
            if (stmt.Condition != null)
            {
                // building the condition
                var cmp = GenerateExpressionCode(stmt.Condition);
                _builder.BuildCondBr(cmp, bbBody, bbEnd);
            }
            else
            {
                // if the second param is null (should not happen!!! - checked in Parsing) - just move to the body block
                _builder.BuildBr(bbBody);
            }            

            // appending them sooner
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);

            _builder.PositionAtEnd(bbEnd);

            // restoring prev blocks
            _currentLoopInc = prevWhileInc;
            _currentLoopEnd = prevWhileEnd;
            _currentLoop = prevLoop;
        }

        private ulong _ifCounter;
        private unsafe void GenerateIfStmt(AstIfStmt stmt)
        {
            // WARN: this strange code is not just for 'fun'
            // when creating nested 'if' stmts it would be easier to read LLVM IR code with that shite
            // so for example if we have two nested 'if' stmts it would look like this:
            // if1.body: ...
            //   if2.body: ...
            //   if2.end: ...
            // if1.else
            //	 ...
            // if1.end: ...

            _ifCounter++;

            var bbBody = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"if{_ifCounter}.body");

            // creating other blocks
            var bbElse = _context.CreateBasicBlock($"if{_ifCounter}.else");
            var bbEnd = _context.CreateBasicBlock($"if{_ifCounter}.end");

            if (stmt.Condition != null)
            {
                var cmp = GenerateExpressionCode(stmt.Condition);
                if (stmt.BodyFalse != null)
                {
                    _builder.BuildCondBr(cmp, bbBody, bbElse);
                }
                else
                {
                    // going directly to end block because there is no else block
                    _builder.BuildCondBr(cmp, bbBody, bbEnd);
                }
            }
            else
            {
                // if the condition is null (should not happen!!! - checked in Parsing) - just move to the body block
                _builder.BuildBr(bbBody);
            }

            // body
            _builder.PositionAtEnd(bbBody);
            // generating body code
            GenerateExpressionCode(stmt.BodyTrue);

            // check if it has br/ret 
            if (!AstBlockExpr.IsBlockHasItsOwnBr(stmt.BodyTrue))
            {
                // setting br without condition into inc block from body block
                _builder.BuildBr(bbEnd);
            }

            // else
            if (stmt.BodyFalse != null)
            {
                LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbElse);
                _builder.PositionAtEnd(bbElse);
                // generating else code
                GenerateExpressionCode(stmt.BodyFalse);

                // check if it has br/ret 
                if (!AstBlockExpr.IsBlockHasItsOwnBr(stmt.BodyFalse))
                {
                    // setting br without condition into inc block from body block
                    _builder.BuildBr(bbEnd);
                }
            }

            // appending them sooner
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);

            _builder.PositionAtEnd(bbEnd);
        }

        private ulong _switchCounter;
        private Dictionary<AstCaseStmt, LLVMBasicBlockRef> _caseBlockDictionary;
        private unsafe void GenerateSwitchStmt(AstSwitchStmt stmt)
        {
            _switchCounter++;

            // checking if there is a user defined default case
            bool userDefinedDefaultCase = stmt.Cases.Any(x => x.IsDefaultCase);

            var prevCaseBlockDic = _caseBlockDictionary;
            var prevLoopEnd = _currentLoopEnd;
            var prevLoop = _currentLoop;

            var bbDefault = _context.CreateBasicBlock($"switch{_switchCounter}.default");
            var bbEnd = _context.CreateBasicBlock($"switch{_switchCounter}.end");

            _currentLoopEnd = bbEnd;
            _currentLoop = stmt;

            var subExprOfSwitch = GenerateExpressionCode(stmt.SubExpression);
            // this cringe shite is because the default case always exists even if user has not defined it!!!
            var theSwitchValueRef = _builder.BuildSwitch(subExprOfSwitch, bbDefault, (uint)(userDefinedDefaultCase ? stmt.Cases.Count : stmt.Cases.Count + 1));

            // counter for the names of the cases
            int caseCounter = 0;
            // need to fill up dictionary on case-bb to handle goto stmts
            _caseBlockDictionary = new Dictionary<AstCaseStmt, LLVMBasicBlockRef>();
            foreach (var cc in stmt.Cases)
            {
                LLVMBasicBlockRef bb;
                if (cc.IsDefaultCase)
                    bb = bbDefault;
                else
                    bb = _context.CreateBasicBlock($"switch{_switchCounter}.case{caseCounter++}");
                _caseBlockDictionary.Add(cc, bb);
            }
            
            // this list holds all the falling cases.
            // when the non-falling occured all the falling are also going to be prepared
            List<AstCaseStmt> fallingCases = new List<AstCaseStmt>();
            foreach (var cc in stmt.Cases)
            {
                // just wait for a normal case
                if (cc.IsFallingCase)
                {
                    fallingCases.Add(cc);
                    continue;
                }

                // creating a block for the case
                LLVMBasicBlockRef currBb = _caseBlockDictionary[cc];
                LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, currBb);
                _builder.PositionAtEnd(currBb);

                // generating the block
                // TODO: the return value could be used for returnable switch-case exprs :))
                var _ = GenerateExpressionCode(cc.Body);

                // check if it has br/ret 
                if (!AstBlockExpr.IsBlockHasItsOwnBr(cc.Body))
                {
                    // setting br into end block from body block
                    _builder.BuildBr(bbEnd);
                }

                // there is no pattern in default case
                if (!cc.IsDefaultCase)
                {
                    // the pattern of the case
                    var patt = GenerateExpressionCode(cc.Pattern);
                    // creating the LLVM case 
                    theSwitchValueRef.AddCase(patt, currBb);
                }

                // going through all the falling cases
                foreach (var fc in fallingCases)
                {
                    // the pattern of the case
                    var pattFc = GenerateExpressionCode(fc.Pattern);
                    // creating the LLVM case 
                    theSwitchValueRef.AddCase(pattFc, currBb);
                }
                // clear the falling cases
                fallingCases.Clear();
            }

            // if user has not been defined its 'default' case
            if (!userDefinedDefaultCase)
            {
                // just braking into end block
                LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbDefault);
                _builder.PositionAtEnd(bbDefault);
                _builder.BuildBr(bbEnd);
            }

            // the end block
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);
            _builder.PositionAtEnd(bbEnd);

            // restoring prev block
            _caseBlockDictionary = prevCaseBlockDic;
            _currentLoopEnd = prevLoopEnd;
            _currentLoop = prevLoop;
        }

        private unsafe void GenerateBreakContStmt(AstBreakContStmt stmt)
        {
            // need to generate finally block moves
            if (_needGoBackVariables.Count > 0)
            {
                var contFunc = stmt.FindContainingFunction();
                // go from last element
                for (int i = _needGoBackVariables.Count - 1; i >= 0; --i)
                {
                    // check if we need to call finallies or not
                    var nearestTry = _tryCatchStatements[i];
                    // if try-catch is not in the same function
                    if (nearestTry.FindContainingFunction() != contFunc)
                        break;
                    // break loop - it is nested _currentLoop, so no need to gen finally calls
                    if (_currentLoop.Scope.IsChildOf(nearestTry.TryBlock.Scope))
                        break;

                    // make the block into which execution will be returned
                    var beforeBrContBlock = _context.CreateBasicBlock($"before.brcont");

                    // set var that finally need to go back
                    var needGoBack = _needGoBackVariables[i];
                    _builder.BuildStore(_lastFunctionValueRef.GetBlockAddress(beforeBrContBlock), needGoBack);
                    // increase amount of go backs
                    _indirectBlockBlocks[i].Add(beforeBrContBlock);
                    // and build br to the finally
                    _builder.BuildBr(_finallyBlocks[i]);

                    // just make the block
                    LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, beforeBrContBlock);
                    _builder.PositionAtEnd(beforeBrContBlock);
                }
            }

            // just generating shite that jumps between blocks :)
            if (stmt.IsBreak)
            {
                if (_currentLoopEnd == null)
                {
                    _messageHandler.ReportMessage(_currentSourceFile.Text, stmt, [], ErrorCode.Get(CTEN.NoLoopToBreak));
                    return;
                }
                _builder.BuildBr(_currentLoopEnd);
            }
            else
            {
                if (_currentLoopInc == null)
                {
                    _messageHandler.ReportMessage(_currentSourceFile.Text, stmt, [], ErrorCode.Get(CTEN.NoLoopToContinue));
                    return;
                }
                _builder.BuildBr(_currentLoopInc);
            }
        }

        private unsafe void GenerateReturnStmt(AstReturnStmt returnStmt)
        {
            if (returnStmt.ReturnExpression != null)
            {
                var result = GenerateExpressionCode(returnStmt.ReturnExpression);
                _builder.BuildStore(result, _lastFunctionReturnHandlerValueRef);
            }

            var contFunc = returnStmt.FindContainingFunction();
            // next bbs handler
            LLVMBasicBlockRef beforeRetBlock;
            // need to generate finally block moves
            if (_needGoBackVariables.Count > 0)
            {
                // go from last element
                for (int i = _needGoBackVariables.Count - 1; i >= 0; --i)
                {
                    // check if we need to call finallies or not
                    var nearestTry = _tryCatchStatements[i];
                    // if try-catch is not in the same function
                    if (nearestTry.FindContainingFunction() != contFunc)
                        break;
                    // make the block into which execution will be returned
                    beforeRetBlock = _context.CreateBasicBlock($"before.return");
                    // set var that finally need to go back
                    var needGoBack = _needGoBackVariables[i];
                    _builder.BuildStore(_lastFunctionValueRef.GetBlockAddress(beforeRetBlock), needGoBack);
                    // increase amount of go backs
                    _indirectBlockBlocks[i].Add(beforeRetBlock);
                    // and build br to the finally
                    _builder.BuildBr(_finallyBlocks[i]);

                    // just make the block
                    LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, beforeRetBlock);
                    _builder.PositionAtEnd(beforeRetBlock);
                }
            }
            // if func has defer
            if (_lastFunctionDeferBasicBlock != default && _currentFunction == contFunc)
            {
                // call function defer
                // make the block into which execution will be returned
                beforeRetBlock = _context.CreateBasicBlock($"before.return");
                // set var that finally need to go back
                _builder.BuildStore(_lastFunctionValueRef.GetBlockAddress(beforeRetBlock), _lastFunctionDeferBasicBlockGoBack);
                // increase amount of go backs
                _lastFunctionDeferIndirectBlocks.Add(beforeRetBlock);
                // and build br to the finally
                _builder.BuildBr(_lastFunctionDeferBasicBlock);
                // just make the block
                LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, beforeRetBlock);
                _builder.PositionAtEnd(beforeRetBlock);
            }

            // return logics
            if (returnStmt.ReturnExpression != null)
            {
                // return value
                var result = _builder.BuildLoad2(HapetTypeToLLVMType(returnStmt.ReturnExpression.OutType), 
                    _lastFunctionReturnHandlerValueRef, "retHandlerLoaded");
                _builder.BuildRet(result);
            }
            else if (_currentFunction.Returns.OutType is VoidType)
            {
                // ret if void
                // PopStackTrace(); // TODO: stack trace
                _builder.BuildRetVoid();
            }
            else
            {
                // error because the func is not void but with a type return
                // but the 'return' statement was not found
                // WARN: should not happen - PP has to handle it
                _builder.BuildRetVoid();
            }
        }

        private void GenerateBaseCtorStmt(AstBaseCtorStmt baseStmt)
        {
            // do not generate for interface or empty
            if ((baseStmt.BaseType == null && !baseStmt.IsThisCtorCall) || (baseStmt.BaseType?.Declaration.IsInterface ?? false))
                return;

            DeclSymbol ctorSymbol;
            List<AstExpression> casts;
            AstArgumentExpr thisArgument;
            if (!baseStmt.IsThisCtorCall)
            {
                string onlyName = baseStmt.BaseType.Declaration.Name.Name.GetClassNameWithoutNamespace();
                var ctorName = $"{onlyName}_ctor";
                List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(baseStmt.Arguments);
                thisArgument = new AstArgumentExpr(baseStmt.ThisArgument);
                argsWithClassParam.Insert(0, thisArgument);
                ctorSymbol = _postPreparer.GetFuncFromCandidates(new AstIdExpr(ctorName), argsWithClassParam, baseStmt.BaseType.Declaration, true, out casts);
            }
            else
            {
                string onlyName = _currentFunction.ContainingParent.Name.Name.GetClassNameWithoutNamespace();
                var ctorName = $"{onlyName}_ctor";
                List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(baseStmt.Arguments);
                thisArgument = new AstArgumentExpr(baseStmt.ThisArgument)
                {
                    ArgumentModificator = baseStmt.ThisArgument.OutType is StructType ? ParameterModificator.Ref : ParameterModificator.None,
                };
                argsWithClassParam.Insert(0, thisArgument);
                ctorSymbol = _postPreparer.GetFuncFromCandidates(new AstIdExpr(ctorName), argsWithClassParam, _currentFunction.ContainingParent, true, out casts);
            }

            // error if ctor not found
            if (ctorSymbol == null)
            {
                _messageHandler.ReportMessage(_currentSourceFile.Text, baseStmt, [HapetType.AsString(baseStmt.BaseType)], ErrorCode.Get(CTEN.CtorWithArgTypesNotFound));
                return;
            }

            // replace with casts to required
            baseStmt.Arguments.ReplaceWithCasts(casts.Skip(1).ToList());

            // args shite
            List<LLVMValueRef> args = new List<LLVMValueRef>();
            args.Add(GenerateExpressionCode(thisArgument));
            foreach (var a in baseStmt.Arguments)
            {
                args.Add(GenerateExpressionCode(a));
            }

            var ctorFunc = _valueMap[ctorSymbol];
            LLVMTypeRef ctorType = _typeMap[ctorSymbol.Decl.Type.OutType];
            _builder.BuildCall2(ctorType, ctorFunc, args.ToArray());
        }

        private void GenerateGotoStmt(AstGotoStmt stmt)
        {
            var bb = _caseBlockDictionary[stmt.CaseToGoInto];
            _builder.BuildBr(bb);
        }
    }
}
