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
                var result = HapetValueToLLVMValue(realExpr.OutType, realExpr.OutValue);
                if (result.Handle.ToInt64() != 0)
                    return result;
            }

            switch (expr)
            {
                // special case at least for 'for' loop
                // when 'for (int i = 0;...)' where 'int i' 
                // would not be handled by blockExpr
                case AstVarDecl varDecl: GenerateVarDeclCode(varDecl); return null;

                case AstBlockExpr blockExpr: return GenerateBlockExprCode(blockExpr);
                case AstUnaryIncDecExpr unExpr: return GenerateUnaryIncDecExprCode(unExpr);
                case AstUnaryExpr unExpr2: return GenerateUnaryExprCode(unExpr2);
                case AstBinaryExpr binExpr: return GenerateBinaryExprCode(binExpr);
                case AstPointerExpr pointerExpr: return GeneratePointerExprCode(pointerExpr, getPtr);
                case AstAddressOfExpr addrExpr: return GenerateAddressOfExprCode(addrExpr);
                case AstIdExpr idExpr: return GenerateIdExpr(idExpr, getPtr);
                case AstNewExpr newExpr: return GenerateNewExpr(newExpr);
                case AstCallExpr callExpr: return GenerateCallExpr(callExpr, getPtr);
                case AstArgumentExpr argExpr: return GenerateArgumentExpr(argExpr);
                case AstCastExpr castExpr: return GenerateCastExpr(castExpr, getPtr);
                case AstNestedExpr nestExpr: return GenerateNestedExpr(nestExpr, getPtr);
                case AstArrayCreateExpr arrayCreateExpr: return GenerateArrayCreateExprCode(arrayCreateExpr, getPtr);
                case AstArrayAccessExpr arrayAccessExpr: return GenerateArrayAccessExprCode(arrayAccessExpr, getPtr);
                case AstTernaryExpr ternaryExpr: return GenerateTernaryExprCode(ternaryExpr);
                case AstCheckedExpr checkedExpr: return GenerateCheckedExprCode(checkedExpr);
                case AstSATOfExpr satExpr: return GenerateSATExprCode(satExpr);
                case AstEmptyStructExpr emptyStructExpr: return GenerateEmptyStructExprCode(emptyStructExpr);
                case AstLambdaExpr lambdaExpr: return GenerateLambdaExprCode(lambdaExpr);

                case AstNullExpr nullExpr: return GenerateNullExprCode(nullExpr);

                // statements
                case AstAssignStmt assignStmt: GenerateAssignStmt(assignStmt); return null;
                case AstForStmt forStmt: GenerateForStmt(forStmt); return null;
                case AstWhileStmt whileStmt: GenerateWhileStmt(whileStmt); return null;
                case AstIfStmt ifStmt: GenerateIfStmt(ifStmt); return null;
                case AstSwitchStmt switchStmt: GenerateSwitchStmt(switchStmt); return null;
                case AstBreakContStmt breakContStmt: GenerateBreakContStmt(breakContStmt); return null;
                case AstReturnStmt returnStmt: GenerateReturnStmt(returnStmt); return null;
                case AstBaseCtorStmt baseStmt: GenerateBaseCtorStmt(baseStmt); return null;
                case AstThrowStmt throwStmt: GenerateThrowStmt(throwStmt); return null;

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

                // check for nested func
                if (stmt is AstFuncDecl)
                {
                    // skip, no need to generate anything
                }
                else
                    GenerateExpressionCode(stmt);
            }
            return result;
        }

        private LLVMValueRef GenerateUnaryExprCode(AstUnaryExpr unExpr)
        {
            if (unExpr.ActualOperator is BuiltInUnaryOperator builtInOp)
            {
                var expr = (unExpr.SubExpr as AstExpression);
                var value = GenerateExpressionCode(expr);
                // return if the value was not properly generated
                if (value == default)
                    return default;

                var uo = GetUnOp(builtInOp);
                var val = uo(_builder, value, "unOp");
                return val;
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

                return _builder.BuildCall2(fncType, fncValue, new LLVMValueRef[] { value }, "unOp");
            }
            _messageHandler.ReportMessage(_currentSourceFile.Text, unExpr, [unExpr.ToString()], ErrorCode.Get(CTEN.StmtNotImplemented));
            return default;
        }

        private LLVMValueRef GenerateUnaryIncDecExprCode(AstUnaryIncDecExpr unExpr)
        {
            if (unExpr.ActualOperator is BuiltInUnaryOperator builtInOp)
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
                return val;

                void MakeOperation()
                {
                    var value = GenerateExpressionCode(expr);
                    var bo = SearchBinOp(unExpr.Operator == "++" ? "+" : "-", expr.OutType, HapetType.CurrentTypeContext.GetIntType(4, true));
                    var toAssign = bo(_builder, value, LLVMValueRef.CreateConstInt(_context.Int32Type, 1), "unOp");
                    var valuePtr = GenerateExpressionCode(expr, true);
                    AssignToVar(valuePtr, toAssign);
                }
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
                return val;

                void MakeOperation()
                {
                    var value = GenerateExpressionCode(expr);
                    var fncType = _typeMap[userDef.Function];
                    var fncValue = _valueMap[userDef.Function.Declaration.Symbol];
                    _builder.BuildCall2(fncType, fncValue, new LLVMValueRef[] { value });
                }
            }
            _messageHandler.ReportMessage(_currentSourceFile.Text, unExpr, [unExpr.ToString()], ErrorCode.Get(CTEN.StmtNotImplemented));
            return default;
        }

        private LLVMValueRef GenerateBinaryExprCode(AstBinaryExpr binExpr)
        {
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
                                    return CreateCast(_builder, left, leftExpr.OutType, rightType);
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
                                        var ptrToCastTypeInfo = _typeInfoDictionary[rightType];
                                        var castTypeNull = LLVMValueRef.CreateConstPointerNull(HapetTypeToLLVMType(PointerType.GetPointerType(rightType)));
                                        var casted = _builder.BuildBitCast(left, HapetTypeToLLVMType(PointerType.GetPointerType(rightType)), "castedAs");

                                        // WARN: hard cock
                                        var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("TypeConverter"));
                                        DeclSymbol downcasterSymbol;
                                        if (rightType.Declaration.IsInterface)
                                            downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("CanBeDowncastedInterface")) as DeclSymbol;
                                        else
                                            downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("CanBeDowncasted")) as DeclSymbol;
                                        var downcasterFunc = _valueMap[downcasterSymbol];
                                        LLVMTypeRef funcType = _typeMap[downcasterSymbol.Decl.Type.OutType];
                                        var canBeDowncasted = _builder.BuildCall2(funcType, downcasterFunc, new LLVMValueRef[] { left, ptrToCastTypeInfo }, "canBeDowncasted");
                                        return _builder.BuildSelect(canBeDowncasted, casted, castTypeNull, "castResult");
                                    }
                                }
                                else
                                {
                                    // just bitcast when upcast shite
                                    return _builder.BuildBitCast(left, HapetTypeToLLVMType(rightExpr.OutType), "castedAs");
                                }
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
                                    return CreateStructCastFromObject(left, rightType, false);
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

                            if (leftExpr.OutType is PointerType pt1 && pt1.TargetType is ClassType leftType)
                            {
                                ClassType rightType;
                                if (rightExpr.OutType is PointerType pt2 && pt2.TargetType is ClassType clsT)
                                {
                                    rightType = clsT;
                                }
                                else
                                {
                                    /// WARN: almost the same as in <see cref="CreateCast"/>
                                    // check cast from object instance to struct

                                    var ptrToCastTypeInfo = _typeInfoDictionary[rightExpr.OutType];

                                    // WARN: hard cock
                                    var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("TypeConverter"));
                                    DeclSymbol downcasterSymbol;
                                    downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("CanBeDowncasted")) as DeclSymbol;
                                    var downcasterFunc = _valueMap[downcasterSymbol];
                                    LLVMTypeRef funcType = _typeMap[downcasterSymbol.Decl.Type.OutType];
                                    var canBeDowncasted = _builder.BuildCall2(funcType, downcasterFunc, new LLVMValueRef[] { left, ptrToCastTypeInfo }, "canBeDowncasted");
                                    return canBeDowncasted;
                                }

                                // you would ask - wtf is anyIsInterface? - read upper 

                                bool anyIsInterface = rightType.Declaration.IsInterface || leftType.Declaration.IsInterface;
                                bool isUpcast = leftType.IsInheritedFrom(rightType);
                                // check upcast
                                if (!isUpcast || anyIsInterface)
                                {
                                    // swap them and check inheritance
                                    bool isDownCast = rightType.IsInheritedFrom(leftType);
                                    if (isDownCast || anyIsInterface)
                                    {
                                        var ptrToCastTypeInfo = _typeInfoDictionary[rightType];

                                        // WARN: hard cock
                                        var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("TypeConverter"));
                                        DeclSymbol downcasterSymbol;
                                        if (rightType.Declaration.IsInterface)
                                            downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("CanBeDowncastedInterface")) as DeclSymbol;
                                        else
                                            downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("CanBeDowncasted")) as DeclSymbol;
                                        var downcasterFunc = _valueMap[downcasterSymbol];
                                        LLVMTypeRef funcType = _typeMap[downcasterSymbol.Decl.Type.OutType];
                                        var canBeDowncasted = _builder.BuildCall2(funcType, downcasterFunc, new LLVMValueRef[] { left, ptrToCastTypeInfo }, "canBeDowncasted");

                                        // if 'is not' cringe - negate
                                        if (binExpr.IsNot)
                                            canBeDowncasted = _builder.BuildNot(canBeDowncasted, "negated");

                                        return canBeDowncasted;
                                    }
                                    else
                                    {
                                        _messageHandler.ReportMessage(_currentSourceFile.Text, binExpr, [HapetType.AsString(leftType), HapetType.AsString(rightType)], ErrorCode.Get(CTEN.TypeCouldNotBeConverted));
                                        return default;
                                    }
                                }
                                else
                                {
                                    // just true when upcast shite
                                    return GenerateExpressionCode(new AstBoolExpr(!binExpr.IsNot));
                                }
                            }
                            else
                            {
                                // cringe shite like 
                                // valueType is ISome

                                var leftValueType = leftExpr.OutType as StructType;
                                if (rightExpr.OutType is PointerType pt3 && pt3.TargetType is ClassType)
                                {
                                    // just false
                                    return GenerateExpressionCode(new AstBoolExpr(binExpr.IsNot));
                                }
                                else if (rightExpr.OutType == leftValueType)
                                {
                                    // just true - it is like
                                    // valueType is TheSameType
                                    return GenerateExpressionCode(new AstBoolExpr(!binExpr.IsNot));
                                }
                                else
                                {
                                    // TODO: error - user tried to do shite like
                                    // valueType is AnotherValueType
                                    return default;
                                }
                            }
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
                                    return GenerateExpressionCode(new AstBoolExpr(binExpr.Operator == "!="));

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
                                return val;
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
                                return val;
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

                return _builder.BuildCall2(fncType, fncValue, new LLVMValueRef[] { left, right }, "binOp");
            }
            _messageHandler.ReportMessage(_currentSourceFile.Text, binExpr, [binExpr.ToString()], ErrorCode.Get(CTEN.StmtNotImplemented));
            return default;
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

        private unsafe LLVMValueRef GenerateNewExpr(AstNewExpr expr)
        {
            LLVMValueRef v = default;
            if (expr.OutType is PointerType pt && pt.TargetType is ClassType classType)
            {
                int structSize = classType.GetSize();

                // getting class ctor
                string onlyName = classType.Declaration.Name.Name.GetClassNameWithoutNamespace();
                var ctorName = $"{onlyName}_ctor";
                List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(expr.Arguments);
                argsWithClassParam.Insert(0, new AstArgumentExpr(new AstIdExpr("this") 
                { 
                    OutType = expr.OutType,
                    Scope = expr.Scope,
                })
                {
                    OutType = expr.OutType,
                    Scope = expr.Scope,
                });
                var ctorSymbol = _postPreparer.GetFuncFromCandidates(new AstIdExpr(ctorName), argsWithClassParam, classType.Declaration, true, out var casts);

                // error if ctor not found
                if (ctorSymbol == null)
                {
                    _messageHandler.ReportMessage(_currentSourceFile.Text, expr.TypeName, [classType.Declaration.Name.Name], ErrorCode.Get(CTEN.CtorWithArgTypesNotFound));
                    return v;
                }

                // replace with casts to required
                expr.Arguments.ReplaceWithCasts(casts.Skip(1).ToList());

                // allocating memory for struct
                v = GetMalloc(structSize, 1);
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

                return v;
            }
            else if (expr.OutType is StructType structType)
            {
                // getting struct ctor
                string onlyName = structType.Declaration.Name.Name.GetClassNameWithoutNamespace();
                var ctorName = $"{onlyName}_ctor";
                List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(expr.Arguments);
                argsWithClassParam.Insert(0, new AstArgumentExpr(new AstIdExpr("this")
                {
                    OutType = structType,
                    Scope = expr.Scope,
                })
                {
                    OutType = structType,
                    Scope = expr.Scope,
                    ArgumentModificator = ParameterModificator.Ref
                });
                var ctorSymbol = _postPreparer.GetFuncFromCandidates(new AstIdExpr(ctorName), argsWithClassParam, structType.Declaration, true, out var casts);

                // error if ctor not found
                if (ctorSymbol == null)
                {
                    _messageHandler.ReportMessage(_currentSourceFile.Text, expr.TypeName, [structType.Declaration.Name.Name], ErrorCode.Get(CTEN.CtorWithArgTypesNotFound));
                    return v;
                }

                // replace with casts to required
                expr.Arguments.ReplaceWithCasts(casts.Skip(1).ToList());

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

                return _builder.BuildLoad2(HapetTypeToLLVMType(structType), v, "ctoredLoaded");
            }

            return v;
        }

        private unsafe LLVMValueRef GenerateCallExpr(AstCallExpr expr, bool getPtr = false)
        {
            // the func is needed to handle virtual shite
            LLVMValueRef CreateCall(LLVMBuilderRef builder, LLVMTypeRef funcType, FunctionType hapetType, LLVMValueRef func, List<LLVMValueRef> args, bool isBaseCall, string name = "")
            {
                if (hapetType.Declaration.ContainingParent is not AstClassDecl clsDecl)
                    return builder.BuildCall2(funcType, func, args.ToArray(), name);

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
                        var vtableInfo = _virtualTableDictionary[clsDecl.Type.OutType as ClassType];
                        var funcToCall = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { args[0], vtableInfo, LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)index) }, "funcToCall");
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

                // args shite
                List<LLVMValueRef> args = new List<LLVMValueRef>();
                if (!expr.StaticCall)
                {
                    // we need to get ptr of var when calling struct func
                    if (expr.TypeOrObjectName.OutType is StructType)
                        args.Add(GenerateExpressionCode(expr.TypeOrObjectName, true));
                    else
                        args.Add(GenerateExpressionCode(expr.TypeOrObjectName));
                }
                // skip the first object param
                var parsToSearch = expr.StaticCall ? fncType.Declaration.Parameters : fncType.Declaration.Parameters.Skip(1).ToList();
                List <AstArgumentExpr> normalArgs = _postPreparer.GenerateNormalArguments(parsToSearch, expr.Arguments, expr);
                foreach (var a in normalArgs)
                {
                    args.Add(GenerateExpressionCode(a));
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
                    LLVMValueRef ret = CreateCall(_builder, funcType, fncType, hapetFunc, args, isBaseCall, $"funcReturnValue");
                    _builder.BuildStore(ret, varPtr);

                    if (getPtr)
                        return varPtr;

                    // need to load it as a ptr
                    var retType = expr.OutType;

                    return _builder.BuildLoad2(HapetTypeToLLVMType(retType), varPtr, "holderLoaded");
                }

                return CreateCall(_builder, funcType, fncType, hapetFunc, args, isBaseCall);
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
                var parsToSearch = expr.StaticCall ? delType.TargetDeclaration.Parameters : delType.TargetDeclaration.Parameters.Skip(1).ToList();
                List<AstArgumentExpr> normalArgs = _postPreparer.GenerateNormalArguments(parsToSearch, expr.Arguments, expr);
                foreach (var a in normalArgs)
                {
                    args.Add(GenerateExpressionCode(a));
                }

                var loadedDelegatePtr = _builder.BuildLoad2(delegateType.GetPointerTo(), hapetDelegate, $"delegateLoadedPtr");
                var loadedDelegate = _builder.BuildLoad2(delegateType, loadedDelegatePtr, $"delegateLoaded");

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
                            var ptrToTypeInfo = _typeInfoDictionary[clsT];
                            var elementIndexValueRef = LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)elementIndex);
                            // WARN: hard cock
                            var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("TypeConverter"));
                            var offseterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("System.Runtime.Conversion.TypeConverter::GetInterfaceOffset(void*:System.Runtime.TypeInfoUnsafe*:int)")) as DeclSymbol;
                            var offseterFunc = _valueMap[offseterSymbol];
                            LLVMTypeRef funcType = _typeMap[offseterSymbol.Decl.Type.OutType];
                            var offset = _builder.BuildCall2(funcType, offseterFunc, new LLVMValueRef[] { leftPart, ptrToTypeInfo, elementIndexValueRef }, "interfaceOffset");

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

            // special case for string for now
            if (expr.ObjectName.OutType is StringType)
            {
                // getting arrayBuf from struct and pointer to it
                LLVMValueRef ptrToArray = GenerateExpressionCode(expr.ObjectName, true);
                var ptrToBuffer = _builder.BuildStructGEP2(HapetTypeToLLVMType(expr.ObjectName.OutType), ptrToArray, 1, "arrayBuf");
                buffer = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType).GetPointerTo(), ptrToBuffer);
            }
            else if (expr.ObjectName.OutType is PointerType)
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

        private unsafe LLVMValueRef GenerateSATExprCode(AstSATOfExpr expr)
        {
            switch (expr.ExprType)
            {
                case TokenType.KwSizeof:
                    return LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)expr.TargetType.OutType.GetSize());
                case TokenType.KwAlignof:
                    return LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)expr.TargetType.OutType.GetAlignment());
                case TokenType.KwNameof:
                    return HapetValueToLLVMValue(HapetType.CurrentTypeContext.StringTypeInstance, expr.TargetType.TryFlatten(null, null));
                case TokenType.KwTypeof:
                    return default; // TODO:
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


            if (stmt.Body != null &&
                stmt.Body.Statements.Count > 0 &&
                (stmt.Body.Statements.Last() is AstReturnStmt ||
                stmt.Body.Statements.Last() is AstBreakContStmt))
            {
                // if the last statement of the block is already
                // a return or break or continue then there is no
                // need to create our own!!!
                // so this case is empty
            }
            else
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

            var bbCond = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"while{_whileCounter}.cond");
            var bbBody = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"while{_whileCounter}.body");

            // creating other blocks
            var bbEnd = _context.CreateBasicBlock($"while{_whileCounter}.end");

            // directly br into loop condition
            _builder.BuildBr(bbCond);

            _currentLoopInc = bbCond; // check upper WARN
            _currentLoopEnd = bbEnd;

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

            if (stmt.Body != null &&
                stmt.Body.Statements.Count > 0 &&
                (stmt.Body.Statements.Last() is AstReturnStmt ||
                stmt.Body.Statements.Last() is AstBreakContStmt))
            {
                // if the last statement of the block is already
                // a return or break or continue then there is no
                // need to create our own!!!
                // so this case is empty
            }
            else
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
                // if the second param is null (should not happen!!! - checked in Parsing) - just move to the body block
                _builder.BuildBr(bbBody);
            }

            // body
            _builder.PositionAtEnd(bbBody);
            if (stmt.BodyTrue != null)
            {
                // generating body code
                GenerateExpressionCode(stmt.BodyTrue);
            }

            if (stmt.BodyTrue != null &&
                stmt.BodyTrue.Statements.Count > 0 &&
                (stmt.BodyTrue.Statements.Last() is AstReturnStmt ||
                stmt.BodyTrue.Statements.Last() is AstBreakContStmt))
            {
                // if the last statement of the block is already
                // a return then there is no
                // need to create our own!!!
                // so this case is empty
            }
            else
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

                if (stmt.BodyFalse.Statements.Count > 0 &&
                    (stmt.BodyFalse.Statements.Last() is AstReturnStmt ||
                    stmt.BodyFalse.Statements.Last() is AstBreakContStmt))
                {
                    // if the last statement of the block is already
                    // a return then there is no
                    // need to create our own!!!
                    // so this case is empty
                }
                else
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
        private unsafe void GenerateSwitchStmt(AstSwitchStmt stmt)
        {
            _switchCounter++;

            // checking if there is a user defined default case
            bool userDefinedDefaultCase = stmt.Cases.Any(x => x.IsDefaultCase);

            var prevLoopEnd = _currentLoopEnd;

            var bbDefault = _context.CreateBasicBlock($"switch{_switchCounter}.default");
            var bbEnd = _context.CreateBasicBlock($"switch{_switchCounter}.end");

            _currentLoopEnd = bbEnd;

            var subExprOfSwitch = GenerateExpressionCode(stmt.SubExpression);
            // this cringe shite is because the default case always exists even if user has not defined it!!!
            var theSwitchValueRef = _builder.BuildSwitch(subExprOfSwitch, bbDefault, (uint)(userDefinedDefaultCase ? stmt.Cases.Count : stmt.Cases.Count + 1));

            // counter for the names of the cases
            int caseCounter = 0;

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
                LLVMBasicBlockRef currBb;
                if (cc.IsDefaultCase)
                {
                    LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbDefault);
                    currBb = bbDefault;
                }
                else
                {
                    currBb = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"switch{_switchCounter}.case{caseCounter++}");
                }
                _builder.PositionAtEnd(currBb);

                // generating the block
                // TODO: the return value could be used for returnable switch-case exprs :))
                var _ = GenerateExpressionCode(cc.Body);

                if (cc.Body != null &&
                    cc.Body.Statements.Count > 0 &&
                    (cc.Body.Statements.Last() is AstReturnStmt ||
                    cc.Body.Statements.Last() is AstBreakContStmt))
                {
                    // if the last statement of the block is already
                    // a return or break or continue then there is no
                    // need to create our own!!!
                    // so this case is empty
                }
                else
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
            _currentLoopEnd = prevLoopEnd;
        }

        private void GenerateBreakContStmt(AstBreakContStmt stmt)
        {
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

        private void GenerateReturnStmt(AstReturnStmt returnStmt)
        {
            LLVMValueRef result = null;
            if (returnStmt.ReturnExpression != null)
                result = GenerateExpressionCode(returnStmt.ReturnExpression);

            // return logics
            if (result != null)
            {
                // return value
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
            if (baseStmt.BaseType == null || baseStmt.BaseType.Declaration.IsInterface)
                return;

            string onlyName = baseStmt.BaseType.Declaration.Name.Name.GetClassNameWithoutNamespace();
            var ctorName = $"{onlyName}_ctor";
            List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(baseStmt.Arguments);
            argsWithClassParam.Insert(0, new AstArgumentExpr(baseStmt.ThisArgument));
            var ctorSymbol = _postPreparer.GetFuncFromCandidates(new AstIdExpr(ctorName), argsWithClassParam, baseStmt.BaseType.Declaration, true, out var casts);

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
            args.Add(GenerateExpressionCode(baseStmt.ThisArgument));
            foreach (var a in baseStmt.Arguments)
            {
                args.Add(GenerateExpressionCode(a));
            }

            var ctorFunc = _valueMap[ctorSymbol];
            LLVMTypeRef ctorType = _typeMap[ctorSymbol.Decl.Type.OutType];
            _builder.BuildCall2(ctorType, ctorFunc, args.ToArray());
        }

        private void GenerateThrowStmt(AstThrowStmt throwStmt)
        {
        }
    }
}
