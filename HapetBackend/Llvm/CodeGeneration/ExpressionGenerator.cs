using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare;
using LLVMSharp.Interop;
using System;
using System.Text;
using System.Xml.Linq;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private LLVMValueRef GenerateExpressionCode(AstStatement expr, bool getPtr = false)
        {
            // if the value already evaluated (usually literals or consts)
            if (expr is AstExpression realExpr && realExpr.OutValue != null)
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
                case AstPointerExpr pointerExpr: return GeneratePointerExprCode(pointerExpr);
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
                case AstEmptyStructExpr emptyStructExpr: return GenerateEmptyStructExprCode(emptyStructExpr);

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
            if (unExpr.ActualOperator is BuiltInUnaryOperator)
            {
                var expr = (unExpr.SubExpr as AstExpression);
                var value = GenerateExpressionCode(expr);
                // return if the value was not properly generated
                if (value == default)
                    return default;

                var uo = GetUnOp(unExpr.Operator, expr.OutType);
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
                var fncValue = _valueMap[userDef.Function.Declaration.GetSymbol];

                return _builder.BuildCall2(fncType, fncValue, new LLVMValueRef[] { value }, "unOp");
            }
            _messageHandler.ReportMessage(_currentSourceFile.Text, unExpr, [unExpr.ToString()], ErrorCode.Get(CTEN.StmtNotImplemented));
            return default;
        }

        private LLVMValueRef GenerateUnaryIncDecExprCode(AstUnaryIncDecExpr unExpr)
        {
            if (unExpr.ActualOperator is BuiltInUnaryOperator)
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
                    var bo = GetBinOp(unExpr.Operator == "++" ? "+" : "-", expr.OutType, IntType.GetIntType(4, true));
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
                    var fncValue = _valueMap[userDef.Function.Declaration.GetSymbol];
                    _builder.BuildCall2(fncType, fncValue, new LLVMValueRef[] { value });
                }
            }
            _messageHandler.ReportMessage(_currentSourceFile.Text, unExpr, [unExpr.ToString()], ErrorCode.Get(CTEN.StmtNotImplemented));
            return default;
        }

        private LLVMValueRef GenerateBinaryExprCode(AstBinaryExpr binExpr)
        {
            if (binExpr.ActualOperator is BuiltInBinaryOperator)
            {
                var leftExpr = (binExpr.Left as AstExpression);
                var left = GenerateExpressionCode(leftExpr);
                // return if the value was not properly generated
                if (left == default)
                    return default;

                // CRINGE :) special cases for as/is/in
                switch (binExpr.ActualOperator.Name)
                {
                    case "as":
                        {
                            var rightExpr = (binExpr.Right as AstExpression);

                            ClassType leftType;
                            ClassType rightType = (rightExpr.OutType as PointerType).TargetType as ClassType;
                            if ((leftExpr.OutType as PointerType).TargetType is ClassType clsT)
                            {
                                leftType = clsT;
                            }
                            else
                            {
                                // when smth like 'valueTyped as ICringeCock'
                                return CreateCast(_builder, left, (leftExpr.OutType as PointerType).TargetType, rightType);
                            }

                            // you would ask - wtf is anyIsInterface?
                            // then I would say:
                            /*
                                class Program
                                {
                                    static void Main() 
                                    {
                                        Obj a = new Obj();
                                        ILoh b = a as ILoh;
                                        Debug.Assert(b != null);
                                    }
                                }

                                interface IBot {}
                                class Obj : IBot {}

                                interface ILoh {}
                                class Base : Obj, ILoh {}

                                class Derived : Base {}
                                */
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
                                    var castTypeNull = LLVMValueRef.CreateConstPointerNull(HapetTypeToLLVMType(rightExpr.OutType));
                                    var casted = _builder.BuildBitCast(left, HapetTypeToLLVMType(rightExpr.OutType), "castedAs");

                                    // WARN: hard cock
                                    var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("TypeConverter"));
                                    DeclSymbol downcasterSymbol;
                                    if (rightType.Declaration.IsInterface)
                                        downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("System.Runtime.Conversion.TypeConverter::CanBeDowncastedInterface(void*:System.Runtime.TypeInfoUnsafe*)")) as DeclSymbol;
                                    else
                                        downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("System.Runtime.Conversion.TypeConverter::CanBeDowncasted(void*:System.Runtime.TypeInfoUnsafe*)")) as DeclSymbol;
                                    var downcasterFunc = _valueMap[downcasterSymbol];
                                    LLVMTypeRef funcType = _typeMap[downcasterSymbol.Decl.Type.OutType];
                                    var canBeDowncasted = _builder.BuildCall2(funcType, downcasterFunc, new LLVMValueRef[] { left, ptrToCastTypeInfo }, "canBeDowncasted");
                                    return _builder.BuildSelect(canBeDowncasted, casted, castTypeNull, "castResult");
                                }
                                else
                                {
                                    _messageHandler.ReportMessage(_currentSourceFile.Text, binExpr, [HapetType.AsString(leftType), HapetType.AsString(rightType)], ErrorCode.Get(CTEN.TypeCouldNotBeConverted));
                                    return default;
                                }
                            }
                            else
                            {
                                // just bitcast when upcast shite
                                return _builder.BuildBitCast(left, HapetTypeToLLVMType(rightExpr.OutType), "castedAs");
                            }
                        }
                    case "is":
                        {
                            var rightExpr = (binExpr.Right as AstExpression);

                            ClassType leftType = (leftExpr.OutType as PointerType).TargetType as ClassType;
                            ClassType rightType;
                            if ((rightExpr.OutType as PointerType).TargetType is ClassType clsT)
                            {
                                rightType = clsT;
                            }
                            else
                            {
                                /// WARN: almost the same as in <see cref="CreateCast"/>
                                // check cast from object instance to struct

                                var ptrToCastTypeInfo = _typeInfoDictionary[(rightExpr.OutType as PointerType).TargetType];

                                // WARN: hard cock
                                var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("TypeConverter"));
                                DeclSymbol downcasterSymbol;
                                downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("System.Runtime.Conversion.TypeConverter::CanBeDowncasted(void*:System.Runtime.TypeInfoUnsafe*)")) as DeclSymbol;
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
                                        downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("System.Runtime.Conversion.TypeConverter::CanBeDowncastedInterface(void*:System.Runtime.TypeInfoUnsafe*)")) as DeclSymbol;
                                    else
                                        downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("System.Runtime.Conversion.TypeConverter::CanBeDowncasted(void*:System.Runtime.TypeInfoUnsafe*)")) as DeclSymbol;
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
                    default:
                        {
                            var rightExpr = (binExpr.Right as AstExpression);
                            var right = GenerateExpressionCode(rightExpr);
                            // return if the value was not properly generated
                            if (right == default)
                                return default;

                            var bo = GetBinOp(binExpr.Operator, leftExpr.OutType, rightExpr.OutType);
                            var val = bo(_builder, left, right, "binOp");
                            return val;
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
                var fncValue = _valueMap[userDef.Function.Declaration.GetSymbol];

                return _builder.BuildCall2(fncType, fncValue, new LLVMValueRef[] { left, right }, "binOp");
            }
            _messageHandler.ReportMessage(_currentSourceFile.Text, binExpr, [binExpr.ToString()], ErrorCode.Get(CTEN.StmtNotImplemented));
            return default;
        }

        private LLVMValueRef GeneratePointerExprCode(AstPointerExpr expr)
        {
            if (expr.IsDereference)
            {
                var theVar = GenerateExpressionCode(expr.SubExpression);
                var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType), theVar, $"derefed");
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
                if (varDecl.ContainingParent is not AstClassDecl classDecl)
                    return v;
                var varName = $"{classDecl.Type.OutType}::{varDecl.Name.Name}";
                v = _module.GetNamedGlobal(varName);
                if (getPtr)
                    return v;
                var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType), v, expr.Name);
                return loaded;
            }
            // this check is done to generate proper delegate
            else if (expr.OutType is FunctionType fncType && theDecl is AstFuncDecl fncDecl)
            {
                // this whole shite is done to create anon delegate of the specified function
                LLVMTypeRef delegateIrType;
                if (_delegateAnonTypes.TryGetValue(fncType.ToCringeString(), out LLVMTypeRef irType))
                {
                    delegateIrType = irType;
                }
                else
                {
                    List<LLVMTypeRef> paramTypes;
                    // creating anon delegate type
                    if (fncType.IsStaticFunction())
                    {
                        // the func is static...
                        paramTypes = fncDecl.Parameters.Select(rt => HapetTypeToLLVMType(rt.Type.OutType)).ToList();
                    }
                    else
                    {
                        // the func is non-static...
                        // skip the first param with class object ptr
                        paramTypes = fncDecl.Parameters.Skip(1).Select(rt => HapetTypeToLLVMType(rt.Type.OutType)).ToList();
                    }
                    var returnType = HapetTypeToLLVMType(fncDecl.Returns.OutType);
                    var funcType = LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), false);

                    // fields of delegate struct
                    var objectPtr = HapetTypeToLLVMType(PointerType.GetPointerType(IntType.GetIntType(1, false))); // ptr to func object
                    var funcPtr = funcType.GetPointerTo();

                    delegateIrType = _context.CreateNamedStruct($"delegate.anon.{fncType.ToCringeString()}");
                    delegateIrType.StructSetBody(new LLVMTypeRef[] {
                            ((LLVMTypeRef)funcPtr),
                            ((LLVMTypeRef)objectPtr)
                        }, false);
                    _delegateAnonTypes[fncType.ToCringeString()] = delegateIrType;
                }
                // by default it is a nullptr
                LLVMValueRef ptrToObject = LLVM.ConstPointerNull(HapetTypeToLLVMType(IntType.GetIntType(1, false)));
                LLVMValueRef ptrToFunc = _valueMap[declSymbol]; // mb ptr to?
                var allocatedDelegate = _builder.BuildAlloca(delegateIrType, "anonAllocated");
                // if it is not a static func - get ptr to class
                if (!fncType.IsStaticFunction())
                {
                    ptrToObject = _valueMap[expr.Scope.GetSymbol(new AstIdExpr("this"))];
                }
                // the 1 is because delegate struct has object field as it's 1 param
                var objPtr = _builder.BuildStructGEP2(delegateIrType, allocatedDelegate, 1, "objectPtr");
                _builder.BuildStore(ptrToObject, objPtr);
                // setting the func ptr
                var funcPtrr = _builder.BuildStructGEP2(delegateIrType, allocatedDelegate, 0, "funcPtr");
                _builder.BuildStore(ptrToFunc, funcPtrr);

                if (getPtr)
                    return allocatedDelegate;
                var loaded = _builder.BuildLoad2(delegateIrType, allocatedDelegate, "anonDelegateLoaded");
                return loaded;
            }
            else
            {
                v = _valueMap[declSymbol];
                // return the ptr to the val. used for AstAddressOf or storing values
                if (getPtr)
                    return v;
                var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType), v, expr.Name);
                return loaded;
            }
        }

        private unsafe LLVMValueRef GenerateNewExpr(AstNewExpr expr)
        {
            LLVMValueRef v = default;
            if (expr.OutType is ClassType classType)
            {
                int structSize = AstDeclaration.GetSizeForAlloc(classType.Declaration.GetAllRawFields());

                // getting class ctor
                string onlyName = classType.Declaration.Name.Name.GetClassNameWithoutNamespace();
                var ctorName = $"{classType.Declaration.Name.Name}::{onlyName}_ctor" + expr.Arguments.GetArgsString(PointerType.GetPointerType(classType));
                List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(expr.Arguments);
                argsWithClassParam.Insert(0, new AstArgumentExpr(new AstIdExpr("this") { OutType = PointerType.GetPointerType(classType) }));
                var ctorSymbol = _postPreparer.GetFuncFromCandidates(new AstIdExpr(ctorName), null, argsWithClassParam, classType.Declaration, out var casts);

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
                var ctorName = $"{structType.Declaration.Name.Name}::{onlyName}_ctor" + expr.Arguments.GetArgsString(PointerType.GetPointerType(structType));
                List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(expr.Arguments);
                argsWithClassParam.Insert(0, new AstArgumentExpr(new AstIdExpr("this") { OutType = PointerType.GetPointerType(structType) }));
                var ctorSymbol = _postPreparer.GetFuncFromCandidates(new AstIdExpr(ctorName), null, argsWithClassParam, structType.Declaration, out var casts);

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
                        var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("System.Runtime.Conversion.VtableHelper::GetInterfaceMethodByIndex(void*:System.Runtime.VirtualTableUnsafe*:int)")) as DeclSymbol;
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
                        var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("System.Runtime.Conversion.VtableHelper::GetMethodByIndex(void*:int)")) as DeclSymbol;
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
                var hapetFunc = _valueMap[fncType.Declaration.GetSymbol];
                LLVMTypeRef funcType = _typeMap[fncType];

                LLVMValueRef varPtr = default;
                if (fncType.Declaration.Returns.OutType is not VoidType)
                    varPtr = CreateLocalVariable(fncType.Declaration.Returns.OutType, "funcRetHolder");

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
                if (fncType.Declaration.Returns.OutType is not VoidType)
                {
                    // save the value
                    LLVMValueRef ret = CreateCall(_builder, funcType, fncType, hapetFunc, args, isBaseCall, $"funcReturnValue");
                    _builder.BuildStore(ret, varPtr);

                    if (getPtr)
                        return varPtr;
                    return _builder.BuildLoad2(HapetTypeToLLVMType(fncType.Declaration.Returns.OutType), varPtr, "holderLoaded");
                }

                return CreateCall(_builder, funcType, fncType, hapetFunc, args, isBaseCall);
            }
            else if (expr.FuncName.OutType is DelegateType delType)
            {
                var hapetDelegate = GenerateIdExpr(expr.FuncName, true);
                LLVMTypeRef delegateType = _typeMap[expr.FuncName.OutType];

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

                var loadedDelegate = _builder.BuildLoad2(delegateType, hapetDelegate, $"delegateLoaded");
                // TODO: also load object pointer when delegate has non-static method :)
                var theRealFuncExtracted = _builder.BuildExtractValue(loadedDelegate, 0, "funcExtracted");
                // getting the function type to call
                var funcType = GetFunctionTypeOfDelegate(delType);

                // the return name has to be empty if ret value of func is void
                // also save the ret value into a var
                if (delType.TargetDeclaration.Returns.OutType is not VoidType)
                {
                    LLVMValueRef ret = _builder.BuildCall2(funcType, theRealFuncExtracted, args.ToArray(), $"delReturnValue");
                    _builder.BuildStore(ret, varPtr);

                    if (getPtr)
                        return varPtr;
                    return _builder.BuildLoad2(HapetTypeToLLVMType(delType.TargetDeclaration.Returns.OutType), varPtr, "holderLoaded");
                }

                return _builder.BuildCall2(funcType, theRealFuncExtracted, args.ToArray());
            }
            else
            {
                _messageHandler.ReportMessage(_currentSourceFile.Text, expr, [HapetType.AsString(expr.FuncName.OutType)], ErrorCode.Get(CTEN.TheTypeIsNotCallable));
                return default;
            }
        }

        private unsafe LLVMValueRef GenerateArgumentExpr(AstArgumentExpr expr)
        {
            return GenerateExpressionCode(expr.Expr);
        }

        private unsafe LLVMValueRef GenerateCastExpr(AstCastExpr expr, bool getPtr = false)
        {
            var sub = GenerateExpressionCode(expr.SubExpression as AstExpression, false);
            var val = CreateCast(_builder, sub, (expr.SubExpression as AstExpression).OutType, expr.OutType);
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
                bool getByRef = (expr.LeftPart.OutType is StructType) || (expr.LeftPart.OutType is ArrayType) || (expr.LeftPart.OutType is StringType);
                var leftPart = GenerateExpressionCode(expr.LeftPart, getByRef);

                // getting struct/class/interface declarations and the type
                HapetType leftPartType = null;
                AstDeclaration leftPartDecl = null;
                List<AstDeclaration> leftPartDeclarations = null;
                if (expr.LeftPart.OutType is PointerType ptr && ptr.TargetType is ClassType classT)
                {
                    leftPartDecl = classT.Declaration;
                    leftPartDeclarations = classT.Declaration.Declarations.Where(x => x is AstVarDecl).ToList();
                    leftPartType = classT;
                }
                else if (expr.LeftPart.OutType is PointerType ptr2 && ptr2.TargetType is StructType strT)
                {
                    leftPartDecl = strT.Declaration;
                    leftPartDeclarations = strT.Declaration.Declarations.Where(x => x is AstVarDecl).ToList();
                    leftPartType = strT;
                }
                // this is usually when accesing static/const values
                // like 'Attribute.CoonstField'
                else if (expr.LeftPart.OutType is ClassType classTT)
                {
                    leftPartDecl = classTT.Declaration;
                    leftPartDeclarations = classTT.Declaration.Declarations.Where(x => x is AstVarDecl).ToList();
                    leftPartType = classTT;
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
                else if (expr.LeftPart.OutType is ArrayType arrayT)
                {
                    leftPartDecl = AstArrayExpr.GetArrayStruct(expr.Scope);
                    leftPartDeclarations = AstArrayExpr.GetArrayStruct(expr.Scope).Declarations;
                    leftPartType = arrayT;
                }
                else if (expr.LeftPart.OutType is StringType stringT)
                {
                    leftPartDecl = AstStringExpr.GetStringStruct(expr.Scope);
                    leftPartDeclarations = AstStringExpr.GetStringStruct(expr.Scope).Declarations;
                    leftPartType = stringT;
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
                        if (theDecl.ContainingParent is not AstClassDecl classDecl)
                            return default;
                        var varName = $"{classDecl.Type.OutType}::{theDecl.Name.Name}";
                        var v = _module.GetNamedGlobal(varName);
                        if (getPtr)
                            return v;
                        var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType), v, $"{idExpr.Name}Loaded");
                        return loaded;
                    }
                    else
                    {
                        // usually this happens when user tries to access non static/const field from a class/struct name
                        if (leftPart == default)
                        {
                            _messageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [idExpr.Name], ErrorCode.Get(CTEN.NonStaticFieldAccess));
                            return default;
                        }

                        // getting the index of the element
                        uint elementIndex = GetElementIndex(idExpr.Name, leftPartDecl);

                        // this is because the first field in class - is it reflection data (?)
                        if (leftPartType is ClassType clsTT && !clsTT.Declaration.IsInterface)
                            elementIndex += 1;

                        // getting normal element index when user used custom struct alignment
                        if (leftPartType is StructType strT && strT.IsUserDefinedAlignment)
                            elementIndex = _structOffsets[strT][elementIndex];

                        var tp = HapetTypeToLLVMType(leftPartType);
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

                            // we need this to also skip TypeInfo ptr at the beginning of the class instance
                            var normalOffset = _builder.BuildAdd(offset, LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)HapetType.CurrentTypeContext.PointerSize), "offsetWithTypeInfoPtr");

                            // get ptr by offset
                            ret = _builder.BuildGEP2(_context.Int8Type, leftPart, new LLVMValueRef[] { normalOffset }, idExpr.Name);
                        }
                        else
                        {
                            ret = _builder.BuildStructGEP2(tp, leftPart, elementIndex, idExpr.Name);
                        }

                        // if we need ptr for the shite. usually used to store some values inside vars
                        if (getPtr)
                            return ret;
                        // loading the field because it is not registered in _typeMap like a normal variable.
                        // it should be ok for all types of the fields including classes and other shite
                        var retLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(idExpr.OutType), ret, $"{idExpr.Name}Loaded");
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
            uint lastFoundIndex = 0;

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

            // for now they are identical
            if (expr.ObjectName.OutType is ArrayType || expr.ObjectName.OutType is StringType)
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

                var retLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType), arrayEl);
                return retLoaded;
            }

            _messageHandler.ReportMessage(_currentSourceFile.Text, expr, [HapetType.AsString(expr.ObjectName.OutType)], ErrorCode.Get(CTEN.ArrayAccessNotGenerate));
            return default;
        }

        private unsafe LLVMValueRef GenerateTernaryExprCode(AstTernaryExpr expr)
        {
            // WARN: almost the same as AstIfStmt!!!

            var bbBody = _lastFunctionValueRef.AppendBasicBlock($"tern.body");

            // creating other blocks
            var bbElse = _context.CreateBasicBlock($"tern.else");
            var bbEnd = _context.CreateBasicBlock($"tern.end");

            // tmp var
            var varPtr = CreateLocalVariable(expr.OutType, "tmpTernVar");

            // building the condition
            var cmp = GenerateExpressionCode(expr.Condition);
            _builder.BuildCondBr(cmp, bbBody, bbElse);

            // body
            _builder.PositionAtEnd(bbBody);
            AssignToVar(varPtr, GenerateExpressionCode(expr.TrueExpr));
            _builder.BuildBr(bbEnd);

            // else
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbElse);
            _builder.PositionAtEnd(bbElse);
            // generating else code
            AssignToVar(varPtr, GenerateExpressionCode(expr.FalseExpr));
            _builder.BuildBr(bbEnd);

            // appending them sooner
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);
            _builder.PositionAtEnd(bbEnd);

            return _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType), varPtr, "ternLoaded");
        }

        private unsafe LLVMValueRef GenerateEmptyStructExprCode(AstEmptyStructExpr expr)
        {
            // getting types
            var structType = expr.TypeForDefault;
            int structSize = AstDeclaration.GetSizeForAlloc(structType.Declaration.GetAllRawFields(), false);
            var allocated = _builder.BuildAlloca(HapetTypeToLLVMType(structType), "allocatedEmpty");

            // making consts
            var zeroLlvm = LLVMValueRef.CreateConstInt(_context.Int32Type, 0);
            var sizeLlvm = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntPtrType.Instance), (ulong)structSize);

            // memset
            var marshalDecl = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("Marshal"));
            var memsetSymbol = (marshalDecl.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("System.Runtime.InteropServices.Marshal::Memset(void*:int:uintptr)")) as DeclSymbol;
            var memsetFunc = _valueMap[memsetSymbol];
            LLVMTypeRef funcType = _typeMap[memsetSymbol.Decl.Type.OutType];
            _builder.BuildCall2(funcType, memsetFunc, new LLVMValueRef[] { allocated, zeroLlvm, sizeLlvm }, "zeroedEmpty");

            var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(structType), allocated, "loadedEmpty");
            return loaded;
        }

        private unsafe LLVMValueRef GenerateNullExprCode(AstNullExpr expr)
        {
            return LLVM.ConstPointerNull(HapetTypeToLLVMType(expr.Target));
        }

        // statements
        private void GenerateAssignStmt(AstAssignStmt stmt)
        {
            LLVMValueRef theVar = GenerateNestedExpr(stmt.Target, true);

            AssignToVar(theVar, stmt.Value);

            // TODO: WARN: Assign is a stmt and does not returns anything. could be changed to expr
            // so stmts like 'a = (b = 3);' would be allowed...
        }

        private static ulong _forCounter;
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

            var bbCond = _lastFunctionValueRef.AppendBasicBlock($"for{_forCounter}.cond");
            var bbBody = _lastFunctionValueRef.AppendBasicBlock($"for{_forCounter}.body");

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

        private static ulong _whileCounter;
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

            var bbCond = _lastFunctionValueRef.AppendBasicBlock($"while{_whileCounter}.cond");
            var bbBody = _lastFunctionValueRef.AppendBasicBlock($"while{_whileCounter}.body");

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

        private static ulong _ifCounter;
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

            var bbBody = _lastFunctionValueRef.AppendBasicBlock($"if{_ifCounter}.body");

            // creating other blocks
            var bbElse = _context.CreateBasicBlock($"if{_ifCounter}.else");
            var bbEnd = _context.CreateBasicBlock($"if{_ifCounter}.end");

            if (stmt.Condition != null)
            {
                // building the condition
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

        private static ulong _switchCounter;
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
                    currBb = _lastFunctionValueRef.AppendBasicBlock($"switch{_switchCounter}.case{caseCounter++}");
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
            var ctorName = $"{baseStmt.BaseType.Declaration.Name.Name}::{onlyName}_ctor" + baseStmt.Arguments.GetArgsString(PointerType.GetPointerType(baseStmt.BaseType));
            List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(baseStmt.Arguments);
            argsWithClassParam.Insert(0, new AstArgumentExpr(baseStmt.ThisArgument));
            var ctorSymbol = _postPreparer.GetFuncFromCandidates(new AstIdExpr(ctorName), null, argsWithClassParam, baseStmt.BaseType.Declaration, out var casts);

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
    }
}
