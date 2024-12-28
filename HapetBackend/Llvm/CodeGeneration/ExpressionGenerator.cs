using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
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
                case AstUnaryExpr unExpr: return GenerateUnaryExprCode(unExpr);
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

                case AstNullExpr nullExpr: return GenerateNullExprCode(nullExpr);

                // statements
                case AstAssignStmt assignStmt: GenerateAssignStmt(assignStmt); return null;
                case AstForStmt forStmt: GenerateForStmt(forStmt); return null;
                case AstWhileStmt whileStmt: GenerateWhileStmt(whileStmt); return null;
                case AstIfStmt ifStmt: GenerateIfStmt(ifStmt); return null;
                case AstSwitchStmt switchStmt: GenerateSwitchStmt(switchStmt); return null;
                case AstBreakContStmt breakContStmt: GenerateBreakContStmt(breakContStmt); return null;
                case AstReturnStmt returnStmt: GenerateReturnStmt(returnStmt); return null;
                // TODO: check other expressions

                default:
                    {
                        _messageHandler.ReportMessage(_currentSourceFile.Text, expr, $"The expr {expr} is not implemented");
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
            // TODO: check other operators (user implemented)
            _messageHandler.ReportMessage(_currentSourceFile.Text, unExpr, $"The expr {unExpr} is not implemented");
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
                            var leftType = leftExpr.OutType as PointerType;
                            // TODO: check inheritance
                            if (true)
                            {
                                //var ptrToTypeInfo = GetTypeInfoPtr(HapetTypeToLLVMType(leftType.TargetType), left);
                                var ptrToCastTypeInfo = _typeInfoDictionary[(rightExpr.OutType as PointerType).TargetType as ClassType];
                                //var result = _builder.BuildAlloca(HapetTypeToLLVMType(rightExpr.OutType), "castResult");
                                //var currentTypeInfo = _builder.BuildAlloca(LLVMTypeRef.CreatePointer(GetTypeInfoType(), 0), "currTypeInfo");
                                //_builder.BuildStore(ptrToTypeInfo, currentTypeInfo);

                                //var bbSuccess = _lastFunctionValueRef.AppendBasicBlock($"cast.success");
                                //var bbCheck = _lastFunctionValueRef.AppendBasicBlock($"cast.check");
                                //var bbFail = _lastFunctionValueRef.AppendBasicBlock($"cast.fail");
                                //var bbLoop = _lastFunctionValueRef.AppendBasicBlock($"cast.loop");
                                //var bbEnd = _lastFunctionValueRef.AppendBasicBlock($"cast.end");

                                //var cmp = _builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, ptrToTypeInfo, ptrToCastTypeInfo);
                                //_builder.BuildCondBr(cmp, bbSuccess, bbCheck);

                                //_builder.PositionAtEnd(bbCheck);
                                //var currLoaded = _builder.BuildLoad2(LLVMTypeRef.CreatePointer(GetTypeInfoType(), 0), currentTypeInfo, "currLoaded");
                                //var parentTypeInfo = GetParentTypeInfoPtr(currLoaded);
                                //_builder.BuildStore(parentTypeInfo, currentTypeInfo); // store new one
                                //var typeInfoNull = LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(GetTypeInfoType(), 0));
                                //var nullCmp = _builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, parentTypeInfo, typeInfoNull);
                                //_builder.BuildCondBr(nullCmp, bbFail, bbLoop);

                                //_builder.PositionAtEnd(bbLoop);
                                //var cmpLoop = _builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, parentTypeInfo, ptrToCastTypeInfo);
                                //_builder.BuildCondBr(cmpLoop, bbSuccess, bbCheck);

                                //_builder.PositionAtEnd(bbFail);
                                var castTypeNull = LLVMValueRef.CreateConstPointerNull(HapetTypeToLLVMType(rightExpr.OutType));
                                //_builder.BuildStore(castTypeNull, result);
                                //_builder.BuildBr(bbEnd);

                                //_builder.PositionAtEnd(bbSuccess);
                                var casted = _builder.BuildBitCast(left, HapetTypeToLLVMType(rightExpr.OutType), "castedAs");
                                //_builder.BuildStore(casted, result);
                                //_builder.BuildBr(bbEnd);

                                //_builder.PositionAtEnd(bbEnd);

                                //return _builder.BuildLoad2(HapetTypeToLLVMType(rightExpr.OutType), result);

                                // WARN: hard cock
                                var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", "TypeConverter");
                                var downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol("System.Runtime.Conversion.TypeConverter::CanBeDowncasted(void*:System.Runtime.TypeInfoUnsafe*)") as DeclSymbol;
                                var downcasterFunc = _valueMap[downcasterSymbol];
                                LLVMTypeRef funcType = _typeMap[downcasterSymbol.Decl.Type.OutType];
                                var canBeDowncasted = _builder.BuildCall2(funcType, downcasterFunc, new LLVMValueRef[] { left, ptrToCastTypeInfo }, "canBeDowncasted");
                                return _builder.BuildSelect(canBeDowncasted, casted, castTypeNull, "castResult");
                            }
                            return _builder.BuildBitCast(left, HapetTypeToLLVMType(rightExpr.OutType), "castedAs");
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
            // TODO: check other operators (user implemented)
            _messageHandler.ReportMessage(_currentSourceFile.Text, binExpr, $"The expr {binExpr} is not implemented");
            return default;
        }

        private LLVMValueRef GeneratePointerExprCode(AstPointerExpr expr)
        {
            if (expr.IsDereference)
            {
                var theVar = GenerateExpressionCode(expr.SubExpression);
                var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(expr.SubExpression.OutType), theVar, $"derefed");
                return loaded;
            }
            else
            {
                // idk what to do here :_(
                // anyway it should not happen...
                // internal error here
                _messageHandler.ReportMessage(_currentSourceFile.Text, expr, $"Internal compiler error (AstPointerExpr could not be generated here)");
            }
            return default;
        }

        private LLVMValueRef GenerateAddressOfExprCode(AstAddressOfExpr addrExpr)
        {
            // TODO: should be better. probably there won't be only AstNestedExpr or AstIdExpr but something else...
            if (addrExpr.SubExpression is AstNestedExpr nestExpr)
            {
                return GenerateNestedExpr(nestExpr, true);
            }
            else if (addrExpr.SubExpression is AstIdExpr idExpr)
            {
                return GenerateIdExpr(idExpr, true);
            }
            // internal error here
            _messageHandler.ReportMessage(_currentSourceFile.Text, addrExpr, $"Internal compiler error (AstAddressOfExpr could not be generated here)");
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
                    ptrToObject = _valueMap[expr.Scope.GetSymbol("this")];
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
                // TODO: some shite with alignment here
                int structSize = 0;
                List<HapetType> structElements = _structTypeElementsMap[classType];
                foreach (var elem in structElements)
                {
                    structSize += elem.GetSize();
                }

                // allocating memory for struct
                v = GetMalloc(structSize, 1);

                // other args
                List<LLVMValueRef> args = new List<LLVMValueRef>() { v };
                foreach (var a in expr.Arguments)
                {
                    args.Add(GenerateExpressionCode(a));
                }

                // getting class ctor
                string onlyName = classType.Declaration.Name.Name.Split('.').Last();
                var ctorName = $"{classType.Declaration.Name.Name}::{onlyName}_ctor" + expr.Arguments.GetArgsString(PointerType.GetPointerType(classType));
                var ctorSymbol = classType.Declaration.SubScope.GetSymbol(ctorName) as DeclSymbol;

                // error if ctor not found
                if (ctorSymbol == null)
                {
                    _messageHandler.ReportMessage(_currentSourceFile.Text, expr.TypeName, $"Constructor with specified argument types was not found in the {classType.Declaration.Name.Name} class");
                    return v;
                }

                var ctorFunc = _valueMap[ctorSymbol];
                LLVMTypeRef ctorType = _typeMap[ctorSymbol.Decl.Type.OutType];
                _builder.BuildCall2(ctorType, ctorFunc, args.ToArray());  // calling ctor

                return v;
            }
            else
            {
                // TODO: other also could be created 
            }

            return v;
        }

        private unsafe LLVMValueRef GenerateCallExpr(AstCallExpr expr, bool getPtr = false)
        {
            // creating a variable to store function result. for what?
            // because in some places in code generation we need for var pointer
            // but if we do not allocate any var - so how would we get the ptr?
            // to solve the problem we implicitly create a varialbe that would contain return value
            // so 'Anime().Length;' -> 'var a = Anime(); a.Length;'
            // WARN! create the var only if the func has non void ret type!!!

            if (expr.FuncName.OutType is FunctionType fncType)
            {
                var hapetFunc = _valueMap[fncType.Declaration.GetSymbol];
                LLVMTypeRef funcType = _typeMap[expr.FuncName.OutType];

                LLVMValueRef varPtr = default;
                if (fncType.Declaration.Returns.OutType is not VoidType)
                    varPtr = CreateLocalVariable(fncType.Declaration.Returns.OutType, "funcRetHolder");

                // args shite
                List<LLVMValueRef> args = new List<LLVMValueRef>();
                if (!expr.StaticCall)
                {
                    args.Add(GenerateExpressionCode(expr.TypeOrObjectName));
                }
                foreach (var a in expr.Arguments)
                {
                    args.Add(GenerateExpressionCode(a));
                }

                // the return name has to be empty if ret value of func is void
                // also save the ret value into a var
                if (fncType.Declaration.Returns.OutType is not VoidType)
                {
                    // save the value
                    LLVMValueRef ret = _builder.BuildCall2(funcType, hapetFunc, args.ToArray(), $"funcReturnValue");
                    _builder.BuildStore(ret, varPtr);

                    if (getPtr)
                        return varPtr;
                    return _builder.BuildLoad2(HapetTypeToLLVMType(fncType.Declaration.Returns.OutType), varPtr, "holderLoaded");
                }

                return _builder.BuildCall2(funcType, hapetFunc, args.ToArray());
            }
            else if (expr.FuncName.OutType is DelegateType delType)
            {
                var hapetDelegate = GenerateIdExpr(expr.FuncName, true);
                LLVMTypeRef delegateType = _typeMap[expr.FuncName.OutType];

                LLVMValueRef varPtr = default;
                if (delType.Declaration.Returns.OutType is not VoidType)
                    varPtr = CreateLocalVariable(delType.Declaration.Returns.OutType, "delRetHolder");

                // args shite
                List<LLVMValueRef> args = new List<LLVMValueRef>();
                foreach (var a in expr.Arguments)
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
                if (delType.Declaration.Returns.OutType is not VoidType)
                {
                    LLVMValueRef ret = _builder.BuildCall2(funcType, theRealFuncExtracted, args.ToArray(), $"delReturnValue");
                    _builder.BuildStore(ret, varPtr);

                    if (getPtr)
                        return varPtr;
                    return _builder.BuildLoad2(HapetTypeToLLVMType(delType.Declaration.Returns.OutType), varPtr, "holderLoaded");
                }

                return _builder.BuildCall2(funcType, theRealFuncExtracted, args.ToArray());
            }
            else
            {
                _messageHandler.ReportMessage(_currentSourceFile.Text, expr, $"Call of {expr.FuncName.OutType} is not supported");
                return default;
            }
        }

        private unsafe LLVMValueRef GenerateArgumentExpr(AstArgumentExpr expr)
        {
            // TODO: handle arg name and index
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
                // if really has to be an AstIdExpr
                if (expr.RightPart is not AstIdExpr idExpr)
                {
                    _messageHandler.ReportMessage(_currentSourceFile.Text, expr.RightPart, $"The part of the expression has to be an identifier");
                    return default;
                }

                // we need to get 'struct' elements by ref to access it's elements
                bool getByRef = (expr.LeftPart.OutType is StructType) || (expr.LeftPart.OutType is ArrayType) || (expr.LeftPart.OutType is StringType);
                var leftPart = GenerateExpressionCode(expr.LeftPart, getByRef);

                // getting struct/class/interface declarations and the type
                HapetType leftPartType = null;
                List<AstDeclaration> leftPartDeclarations = null;
                if (expr.LeftPart.OutType is PointerType ptr && ptr.TargetType is ClassType classT)
                {
                    leftPartDeclarations = classT.Declaration.Declarations.Where(x => x is AstVarDecl).ToList();
                    leftPartType = classT;
                }
                // this is usually when accesing static/const values
                // like 'Attribute.CoonstField'
                else if (expr.LeftPart.OutType is ClassType classTT)
                {
                    leftPartDeclarations = classTT.Declaration.Declarations.Where(x => x is AstVarDecl).ToList();
                    leftPartType = classTT;
                }
                else if (expr.LeftPart.OutType is StructType structT)
                {
                    leftPartDeclarations = structT.Declaration.Declarations;
                    leftPartType = structT;
                }
                else if (expr.LeftPart.OutType is EnumType enumT)
                {
                    leftPartDeclarations = enumT.Declaration.Declarations.Select(x => x as AstDeclaration).ToList();
                    leftPartType = enumT;
                }
                else if (expr.LeftPart.OutType is ArrayType arrayT)
                {
                    leftPartDeclarations = AstArrayExpr.ArrayStruct.Declarations;
                    leftPartType = arrayT;
                }
                else if (expr.LeftPart.OutType is StringType stringT)
                {
                    leftPartDeclarations = AstStringExpr.StringStruct.Declarations;
                    leftPartType = stringT;
                }

                // getting index of the element and the element itself
                if (leftPartDeclarations != null && leftPartType != null)
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
                            _messageHandler.ReportMessage(_currentSourceFile.Text, idExpr, $"The element '{idExpr.Name}' could not be accessed. You are probably trying to access a non static/const field");
                            return default;
                        }

                        // getting the index of the element
                        uint elementIndex = GetElementIndex(idExpr.Name, leftPartDeclarations);

                        // this is because the first field in class - is it reflection data (?)
                        if (leftPartType is ClassType)
                            elementIndex += 1;

                        // getting normal element index when user used custom struct alignment
                        if (leftPartType is StructType strT && strT.IsUserDefinedAlignment)
                            elementIndex = _structOffsets[strT][elementIndex];

                        var tp = HapetTypeToLLVMType(leftPartType);
                        var ret = _builder.BuildStructGEP2(tp, leftPart, elementIndex, idExpr.Name);
                        // if we need ptr for the shite. usually used to store some values inside vars
                        if (getPtr)
                            return ret;
                        // loading the field because it is not registered in _typeMap like a normal variable.
                        // it should be ok for all types of the fields including classes and other shite
                        var retLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(idExpr.OutType), ret, $"{idExpr.Name}Loaded");
                        return retLoaded;
                    }
                }

                // TODO: strings and other
            }
            _messageHandler.ReportMessage(_currentSourceFile.Text, expr, $"The nested expr could not be generated, fatal :^( ");
            return default;
        }

        private bool IsStaticOrConstElement(string name, List<AstDeclaration> decls, out AstVarDecl decl)
        {
            // getting pure decls with consts and statics
            var pureDecls = decls.Where(x => x.SpecialKeys.Contains(TokenType.KwStatic) || x.SpecialKeys.Contains(TokenType.KwConst)).ToList();
            decl = pureDecls.FirstOrDefault(x => x.Name.Name == name) as AstVarDecl;
            return decl != null;
        }

        private uint GetElementIndex(string name, List<AstDeclaration> decls)
        {
            // getting pure decls without consts and statics
            var pureDecls = decls.Where(x => !x.SpecialKeys.Contains(TokenType.KwStatic) && !x.SpecialKeys.Contains(TokenType.KwConst)).ToList();
            // search for the name in decl
            for (uint i = 0; i < pureDecls.Count; ++i)
            {
                var decl = pureDecls[(int)i];
                if (decl.Name.Name == name)
                {
                    return i; // getting the field index
                }
            }
            return 0;
        }

        private LLVMValueRef GenerateArrayCreateExprCode(AstArrayCreateExpr expr, bool getPtr = false)
        {
            // TODO: check if it could be allocated on stack

            var cloned = expr.Clone() as AstArrayCreateExpr;
            return GenerateArrayInternal(cloned, getPtr);
        }

        private LLVMValueRef GenerateArrayAccessExprCode(AstArrayAccessExpr expr, bool getPtr = false)
        {
            if (expr.ParameterExpr.OutType is not IntType)
            {
                // error here? i cannot access array if it is not an int type
                _messageHandler.ReportMessage(_currentSourceFile.Text, expr.ParameterExpr, $"Type of the index has to be an integer type");
            }

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

            _messageHandler.ReportMessage(_currentSourceFile.Text, expr, $"Could not generate access code for the {expr.ObjectName.OutType} type");
            return default;
        }

        private unsafe LLVMValueRef GenerateNullExprCode(AstNullExpr expr)
        {
            return LLVM.ConstPointerNull(HapetTypeToLLVMType(expr.Target));
        }

        // statements
        private void GenerateAssignStmt(AstAssignStmt stmt)
        {
            LLVMValueRef theVar = GenerateNestedExpr(stmt.Target, true);

            // check for initializer
            if (stmt.Value == null)
            {
                // error here!!!!! it could not be null
                _messageHandler.ReportMessage(_currentSourceFile.Text, stmt, $"Expression expected on the right side of assignment");
            }

            AssignToVar(theVar, stmt.Target.OutType, stmt.Value);

            // TODO: WARN: Assign is a stmt and does not returns anything. could be changed to expr
            // so stmts like 'a = (b = 3);' would be allowed...
        }

        private static ulong _forCounter = 0;
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

            if (stmt.FirstParam != null)
                GenerateExpressionCode(stmt.FirstParam);

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
            if (stmt.SecondParam != null)
            {
                // building the condition
                var cmp = GenerateExpressionCode(stmt.SecondParam);
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
            if (stmt.ThirdParam != null)
            {
                // generating inc code
                GenerateExpressionCode(stmt.ThirdParam);
            }
            _builder.BuildBr(bbCond);
            _builder.PositionAtEnd(bbEnd);

            // restoring prev blocks
            _currentLoopInc = prevForInc;
            _currentLoopEnd = prevForEnd;
        }

        private static ulong _whileCounter = 0;
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
            if (stmt.ConditionParam != null)
            {
                // building the condition
                var cmp = GenerateExpressionCode(stmt.ConditionParam);
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

        private static ulong _ifCounter = 0;
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

                if (stmt.BodyFalse != null &&
                    stmt.BodyFalse.Statements.Count > 0 &&
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

        private static ulong _switchCounter = 0;
        private unsafe void GenerateSwitchStmt(AstSwitchStmt stmt)
        {
            _switchCounter++;

            // checking if there is a user defined default case
            bool userDefinedDefaultCase = stmt.Cases.Any(x => x.DefaultCase);

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
                if (cc.FallingCase)
                {
                    fallingCases.Add(cc);
                    continue;
                }

                // creating a block for the case
                LLVMBasicBlockRef currBb;
                if (cc.DefaultCase)
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
                if (!cc.DefaultCase)
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
                    _messageHandler.ReportMessage(_currentSourceFile.Text, stmt, $"Loop/switch to break could not be found");
                    return;
                }
                _builder.BuildBr(_currentLoopEnd);
            }
            else
            {
                if (_currentLoopInc == null)
                {
                    _messageHandler.ReportMessage(_currentSourceFile.Text, stmt, $"Loop to continue could not be found");
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
                _messageHandler.ReportMessage(_currentSourceFile.Text, returnStmt, "The 'return' statement returns a type that does not match the type specified in the function declaration");
                _builder.BuildRetVoid();
            }
        }
    }
}
