using System;
using System.IO;
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
using HapetLastPrepare;
using HapetPostPrepare;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private ProgramFile _currentSourceFile;
        private AstFuncDecl _currentFunction;

        private void GenerateCode()
        {
            var funcDecls = _postPreparer.AllFunctionsMetadata.ToList();
            var funcs = new Dictionary<AstFuncDecl, LLVMTypeRef>();

            foreach (var func in funcDecls)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(func))
                    continue;

                if (IsFunctionShouldBeSkipped(func))
                    continue;

                _currentSourceFile = func.SourceFile;
                // defining global func
                var funcType = HapetTypeToLLVMType(func.Type.OutType);
                funcs.Add(func, funcType);
            }

            foreach (var l in _compiler.LambdasAndNested) 
            {
                if (l is not AstLambdaExpr lambda)
                    continue;
                GenerateLambdaExprCodeMain(lambda);
            }
            foreach (var (funcDecl, funcType) in funcs)
            {
                _currentSourceFile = funcDecl.SourceFile;
                GenerateFuncCode(funcDecl, funcType);
            }
        }

        private LLVMValueRef _lastFunctionValueRef;
        private LLVMValueRef _lastFunctionReturnHandlerValueRef;

        private LLVMBasicBlockRef _lastFunctionDeferBasicBlock;
        private LLVMValueRef _lastFunctionDeferBasicBlockGoBack;
        private List<LLVMBasicBlockRef> _lastFunctionDeferIndirectBlocks = new List<LLVMBasicBlockRef>();
        private unsafe void GenerateFuncCode(AstFuncDecl funcDecl, LLVMTypeRef? funcType = null, bool forMetadata = false)
        {
            _currentFunction = funcDecl;

            // which imported functions has to be generated with body
            var allowFunctionBodyToBeGenerated = funcDecl.IsImplOfGeneric || (funcDecl.ContainingParent?.IsImplOfGeneric ?? false) || 
                (funcDecl.IsNestedDecl && funcDecl.ParentDecl.IsImplOfGeneric) || 
                ((funcDecl.ContainingParent?.IsNestedDecl ?? false) && funcDecl.ContainingParent.ParentDecl.IsImplOfGeneric);

            funcType ??= HapetTypeToLLVMType(funcDecl.Type.OutType);            

            // if it is for metadata - only 'declares' would be generated that would be replaced with 'defines' in the future
            if (forMetadata)
            {
                // special case for inlined extern functions
                if (funcDecl.SpecialKeys.Contains(TokenType.KwInline) && funcDecl.SpecialKeys.Contains(TokenType.KwExtern))
                {
                    GenerateInlinedExternFunction(funcDecl, funcType.Value);
                    return;
                }
                
                // making cool name
                string funcName = GenericsHelper.GetCodegenFunctionName(funcDecl, _messageHandler);

                // declaring global func
                LLVMValueRef lfunc = _module.AddFunction(funcName, funcType.Value);

                if (funcDecl.IsImported && !allowFunctionBodyToBeGenerated)
                {
                    // this is an imported function from another assembly
                    lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
                    lfunc.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;

                    // WARN: do I need to define calling convention here like below?
                }
                else if (!funcDecl.SpecialKeys.Contains(TokenType.KwUnreflected))
                {
                    // make the function dllexport when it is not 'unreflected'
                    lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
                    lfunc.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLExportStorageClass;

                    // for win-x86 callconv is that
                    if (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform == HapetFrontend.TargetPlatform.Win86)
                    {
                        lfunc.FunctionCallConv = (uint)LLVMCallConv.LLVMCCallConv; // cdecl
                    }
                }
                else
                {
                    // unreflected function
                    lfunc.Linkage = LLVMLinkage.LLVMInternalLinkage;
                }

                // check inline attr
                if (funcDecl.SpecialKeys.Contains(TokenType.KwInline))
                {
                    // 3 - is AlwaysInline
                    // https://github.com/dotnet/LLVMSharp/blob/fb8f621699da07ed3244f75142c3cb37a7f49d2f/sources/LLVMSharp/Attribute.cs#L32
                    var attr = LLVM.CreateEnumAttribute(_context, (uint)3, default);
                    LLVM.AddAttributeAtIndex(lfunc, LLVMAttributeIndex.LLVMAttributeFunctionIndex, attr);
                }

                // caching the function	
                _valueMap[funcDecl.Symbol] = lfunc;
                _lastFunctionValueRef = lfunc;

                // setting parameter names
                for (int i = 0; i < funcDecl.Parameters.Count; ++i)
                {
                    var p = funcDecl.Parameters[i];
                    if (p.Name != null)
                        lfunc.GetParams()[i].Name = p.Name.Name;
                }
            }
            else
            {
                // special case for inlined extern functions - just skip body gen for them
                if (funcDecl.SpecialKeys.Contains(TokenType.KwInline) && funcDecl.SpecialKeys.Contains(TokenType.KwExtern))
                    return;
                // skip imported funcs that are not generics
                if (funcDecl.IsImported && !allowFunctionBodyToBeGenerated)
                    return;
                // skip interface funcs
                if (funcDecl.ContainingParent is AstClassDecl clsD && clsD.IsInterface)
                    return;
                // skip funcs that are used only for declaration
                if (funcDecl.IsDeclarationUsedOnlyDeclare)
                {
                    _builder.PositionAtEnd(_valueMap[funcDecl.Symbol].AppendBasicBlockInContext(_context, "no.way"));
                    _builder.BuildUnreachable();
                    return;
                }

                // getting the func
                LLVMValueRef lfunc = _valueMap[funcDecl.Symbol];
                _lastFunctionValueRef = lfunc;

                // check if there is no implementation and it is not an extern shite
                if (funcDecl.Body == null && !funcDecl.SpecialKeys.Contains(TokenType.KwExtern))
                    return;

                // clean up some defer shite
                _lastFunctionDeferBasicBlock = default;
                _lastFunctionDeferBasicBlockGoBack = default;
                _lastFunctionDeferIndirectBlocks.Clear();

                // params body
                var paramsBody = lfunc.AppendBasicBlockInContext(_context, "params");
                _builder.PositionAtEnd(paramsBody);
                // generating params allocs
                for (int i = 0; i < funcDecl.Parameters.Count; ++i)
                {
                    var p = funcDecl.Parameters[i];
                    // skip this shite
                    if (p.ParameterModificator == ParameterModificator.Arglist)
                        continue;

                    // if it is a ref or out or a class - need to make it as a ptr
                    var paramType = p.Type.OutType;
                    if (p.ParameterModificator == ParameterModificator.Ref ||
                        p.ParameterModificator == ParameterModificator.Out)
                        paramType = PointerType.GetPointerType(paramType);

                    var addrAlloca = _builder.BuildAlloca(HapetTypeToLLVMType(paramType), $"{p.Name.Name}.addr");
                    _builder.BuildStore(lfunc.GetParam((uint)i), addrAlloca);
                    _valueMap[p.Symbol] = addrAlloca;
                }
                var configBody = lfunc.AppendBasicBlockInContext(_context, "config");
                _builder.BuildBr(configBody);
                _builder.PositionAtEnd(configBody);

                // need to create return handler here because it is accessable in all blocks in func
                if (funcDecl.Returns.OutType is not VoidType)
                    _lastFunctionReturnHandlerValueRef = CreateLocalVariable(funcDecl.Returns.OutType, "returnHandler");

                // some functions require suppress of defer/stacktrace
                bool doSuppressStackTrace = funcDecl.Attributes.FirstOrDefault(x =>
                    (x.AttributeName.OutType as ClassType).Declaration.NameWithNs == "System.SuppressStackTraceAttribute") != null;

                // if stacktrace is not suppressed then make "exception-handler" to call defer on exception
                if (doSuppressStackTrace)
                {
                    // function body
                    var bbBody = lfunc.AppendBasicBlockInContext(_context, "entry");
                    _builder.BuildBr(bbBody);
                    _builder.PositionAtEnd(bbBody);
                }
                else
                {
                    // this variable will contain 'ptr' when defer has to go back using 'indirectbr'
                    _lastFunctionDeferBasicBlockGoBack = CreateLocalVariable(HapetType.CurrentTypeContext.PtrToVoidType, "needGoBackDefer");
                    var nullValue = LLVMValueRef.CreateConstPointerNull(HapetTypeToLLVMType(HapetType.CurrentTypeContext.PtrToVoidType));
                    _builder.BuildStore(nullValue, _lastFunctionDeferBasicBlockGoBack);

                    _lastFunctionDeferBasicBlock = _context.CreateBasicBlock($"func.defer");

                    var bbException = _context.CreateBasicBlock("on.exception");
                    // make the block into which execution will be returned
                    var beforeRetBlock = _context.CreateBasicBlock($"after.defer.on.exception");
                    var bbBody = _context.CreateBasicBlock("entry");

                    // similar to try-catch generation
                    // making jmp shite here
                    var jmpBuf = CreateJmpBuffer();
                    PushJmpBuf(jmpBuf);
                    var setJmpResult = CreateSetJmpCall(jmpBuf);

                    // compare to 1
                    var binOp = SearchBinOp("==", HapetType.CurrentTypeContext.GetIntType(4, true), HapetType.CurrentTypeContext.GetIntType(4, true));
                    var resCmp = binOp(_builder, setJmpResult, LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)1), "cmpResult");
                    _builder.BuildCondBr(resCmp, bbException, bbBody); // if 0 - normal func, if 1 - call defer and rethrow

                    LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbException);
                    _builder.PositionAtEnd(bbException);
                    // call function defer
                    // set var that finally need to go back
                    _builder.BuildStore(_lastFunctionValueRef.GetBlockAddress(beforeRetBlock), _lastFunctionDeferBasicBlockGoBack);
                    // increase amount of go backs
                    _lastFunctionDeferIndirectBlocks.Add(beforeRetBlock);
                    // and build br to the finally
                    _builder.BuildBr(_lastFunctionDeferBasicBlock);

                    LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, beforeRetBlock);
                    _builder.PositionAtEnd(beforeRetBlock);
                    // making rethrow
                    GenerateThrowStmt(null, true);

                    // go generate normal function body
                    LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbBody);
                    _builder.PositionAtEnd(bbBody);
                }

                // different behaviour when extern func
                if (funcDecl.SpecialKeys.Contains(TokenType.KwExtern))
                {
                    // when extern func
                    GenerateExternFunctionBody(funcDecl);
                }
                else
                {
                    // if not suppress defer/stacktrace
                    if (!doSuppressStackTrace)
                    {
                        // set up defer shite
                        // making cool name
                        string funcName = _postPreparer.GetFuncNameAsOriginal(funcDecl); // TODO: also add namespace, class and params
                        if (funcDecl.IsNestedDecl)
                        {
                            string parentFuncName = _postPreparer.GetFuncNameAsOriginal(funcDecl.ParentDecl as AstFuncDecl);
                            string containingParent = _postPreparer.GetNameFromAst(funcDecl.ParentDecl.ContainingParent.Name, _compiler.MessageHandler);
                            string nameSpace = funcDecl.ParentDecl.ContainingParent.SourceFile.Namespace;
                            funcName = $"{nameSpace}.{containingParent}.{parentFuncName}.{funcName}";
                        }
                        else
                        {
                            string containingParent = _postPreparer.GetNameFromAst(funcDecl.ContainingParent.Name, _compiler.MessageHandler);
                            string nameSpace = funcDecl.ContainingParent.SourceFile.Namespace;
                            funcName = $"{nameSpace}.{containingParent}.{funcName}";
                        }
                        PushStackTrace(funcName);
                    }

                    // genereting inside stuff of the function
                    GenerateBlockExprCode(funcDecl.Body);
                    // if function return type is void and there is no br/ret and the end - add it
                    if (!AstBlockExpr.IsBlockHasItsOwnBr(funcDecl.Body))
                    {
                        if (funcDecl.Returns.OutType is VoidType)
                            GenerateReturnStmt(new AstReturnStmt(null) { Parent = funcDecl }); // need to call the func because of defer
                        else
                            _builder.BuildUnreachable(); /// it should be safe because of <see cref="PostPrepare.CheckThatThereIsEnoughReturnsInFunc(AstFuncDecl)"/>
                    }

                    // if not suppress defer/stacktrace
                    if (!doSuppressStackTrace)
                    {
                        // make up defer shite
                        LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, _lastFunctionDeferBasicBlock);
                        _builder.PositionAtEnd(_lastFunctionDeferBasicBlock);
                        {
                            // pop stacktrace
                            PopStackTrace();
                            // popping current jmpbuf 
                            PopJmpBuf();

                            // go back 
                            var needGoBackLoadedAsPtr = _builder.BuildLoad2(HapetTypeToLLVMType(HapetType.CurrentTypeContext.PtrToVoidType), _lastFunctionDeferBasicBlockGoBack);
                            var indirectBr = _builder.BuildIndirectBr(needGoBackLoadedAsPtr, (uint)_lastFunctionDeferIndirectBlocks.Count);
                            foreach (var bl in _lastFunctionDeferIndirectBlocks)
                                indirectBr.AddDestination(bl);
                        }
                    }
                }

                if (!lfunc.VerifyFunction(LLVMVerifierFailureAction.LLVMReturnStatusAction))
                {
                    _messageHandler.ReportMessage([$"Failed to validate function '{lfunc.Name}'"], ErrorCode.Get(CTEN.LLVMValidateError), ReportType.Error);
                }
            }
        }

        private void GenerateVarDeclCode(AstVarDecl varDecl)
        {
            // alloca new var in basicBlock
            var varPtr = CreateLocalVariable(varDecl.Type.OutType, varDecl.Name.Name);

            // check for initializer and try to evaluate expr
            if (varDecl.Initializer != null)
            {
                AssignToVar(varPtr, varDecl.Initializer);
            }

            // _refMap[varDecl.GetSymbol] = varPtr;
            // _valueMap[varDecl.GetSymbol] = _builder.BuildLoad2(HapetTypeToLLVMType(varDecl.Type.OutType), varPtr, varDecl.Name.Name);
            _valueMap[varDecl.Symbol] = varPtr;
        }

        private void GenerateLambdaExprCodeMain(AstLambdaExpr lambda)
        {
            // making function
            List<LLVMTypeRef> paramTypes = lambda.Parameters.Select(x => HapetTypeToLLVMType(x.Type.OutType)).ToList();
            LLVMTypeRef returnType = HapetTypeToLLVMType(lambda.Returns.OutType);
            LLVMTypeRef funcType = LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), false);
            LLVMValueRef lfunc = _module.AddFunction($"lambda_{lambda.ToCringeString()}", funcType);
            lfunc.Linkage = LLVMLinkage.LLVMInternalLinkage;
            // setting parameter names
            for (int i = 0; i < lambda.Parameters.Count; ++i)
            {
                var p = lambda.Parameters[i];
                if (p.Name != null) lfunc.GetParams()[i].Name = p.Name.Name;
            }
            // params body
            var paramsBody = lfunc.AppendBasicBlockInContext(_context, "params");
            _builder.PositionAtEnd(paramsBody);
            // generating params allocs
            for (int i = 0; i < lambda.Parameters.Count; ++i)
            {
                var p = lambda.Parameters[i];
                var paramType = p.Type.OutType;
                var addrAlloca = _builder.BuildAlloca(HapetTypeToLLVMType(paramType), $"{p.Name.Name}.addr");
                _builder.BuildStore(lfunc.GetParam((uint)i), addrAlloca);
                _valueMap[p.Symbol] = addrAlloca;
            }
            // function body
            var bbBody = lfunc.AppendBasicBlockInContext(_context, "entry");
            _builder.BuildBr(bbBody);
            _builder.PositionAtEnd(bbBody);

            // checking for 'return' existance at the end. if not - add
            if (lambda.Body != null &&
                lambda.Body.Statements.LastOrDefault() is not AstReturnStmt)
            {
                lambda.Body.Statements.Add(new AstReturnStmt(null));
            }
            GenerateBlockExprCode(lambda.Body);

            if (!lfunc.VerifyFunction(LLVMVerifierFailureAction.LLVMReturnStatusAction))
                _messageHandler.ReportMessage([$"Failed to validate function '{lfunc.Name}'"], ErrorCode.Get(CTEN.LLVMValidateError), ReportType.Error);

            _valueMap[lambda.Symbol] = lfunc;
        }

        private void GenerateInlinedExternFunction(AstFuncDecl funcDecl, LLVMTypeRef funcType)
        {
            string dllImportAttrFullName = "System.Runtime.InteropServices.DllImportAttribute"; // WARN: hard cock
            var dllImportAttr = funcDecl.GetAttribute(dllImportAttrFullName);
            // many checks are here
            if (dllImportAttr == null)
            {
                // TODO: check in PP
                _messageHandler.ReportMessage(_currentSourceFile, funcDecl, [], ErrorCode.Get(CTEN.ExternFuncNoAttr));
                return;
            }
            string dllName = dllImportAttr.Arguments[0].OutValue as string;
            string entryPoint = dllImportAttr.Arguments[1].OutValue as string;
            bool isSupp = false;
            if (dllImportAttr.Arguments.Count > 2 && dllImportAttr.Arguments[2].OutValue is bool b)
                isSupp = b;

            // declaring global func
            LLVMValueRef lfunc = _module.GetOrCreateFunction(entryPoint, funcType);
            lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
            // check if suppress dllimport attr
            if (!isSupp) lfunc.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;

            // setting parameter names
            for (int i = 0; i < funcDecl.Parameters.Count; ++i)
            {
                var p = funcDecl.Parameters[i];
                if (p.Name != null)
                    lfunc.GetParams()[i].Name = p.Name.Name;
            }
            // caching the function	
            _valueMap[funcDecl.Symbol] = lfunc;
            _lastFunctionValueRef = lfunc;
        }

        private void GenerateExternFunctionBody(AstFuncDecl funcDecl)
        {
            // creating extern call of C func
            string dllImportAttrFullName = "System.Runtime.InteropServices.DllImportAttribute"; // WARN: hard cock
            var dllImportAttr = funcDecl.GetAttribute(dllImportAttrFullName);

            // many checks are here
            if (dllImportAttr == null)
            {
                // TODO: check in PP
                _messageHandler.ReportMessage(_currentSourceFile, funcDecl, [], ErrorCode.Get(CTEN.ExternFuncNoAttr));
                return;
            }
            string dllName = dllImportAttr.Arguments[0].OutValue as string;
            string entryPoint = dllImportAttr.Arguments[1].OutValue as string;
            bool isSupp = false;
            if (dllImportAttr.Arguments.Count > 2 && dllImportAttr.Arguments[2].OutValue is bool b)
                isSupp = b;

            HapetType vaListType = HapetType.CurrentTypeContext.VaListTypeInstance;
            HapetType ptrToVaListType = PointerType.GetPointerType(vaListType);
            LLVMTypeRef funcType;
            LLVMValueRef funcValue;
            LLVMValueRef apAlloca = default;
            LLVMValueRef apAllocaBitcasted = default;

            // check if there is a dll to be linked with!
            if (!string.IsNullOrWhiteSpace(dllName))
                _libsToBeLinked.Add(dllName);

            // the same type
            /// almost the same as in <see cref="HapetTypeToLLVMType"/>
            var f = funcDecl.Type.OutType as FunctionType;
            var paramTypes = f.Declaration.Parameters.Select(rt => 
                HapetTypeToLLVMType(rt.ParameterModificator == ParameterModificator.Arglist ? ptrToVaListType : rt.Type.OutType)).ToList();
            var returnType = HapetTypeToLLVMType(f.Declaration.Returns.OutType);
            funcType = LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), false);

            // declaring external global func
            funcValue = _module.AddFunction(entryPoint, funcType);
            funcValue.Linkage = LLVMLinkage.LLVMExternalLinkage;
            // check if suppress dllimport attr
            if (!isSupp) funcValue.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;

            // setting parameter names
            for (int i = 0; i < funcDecl.Parameters.Count; ++i)
            {
                var p = funcDecl.Parameters[i];
                if (p.Name != null)
                    funcValue.GetParams()[i].Name = p.Name.Name;
            }

            // generating params
            List<LLVMValueRef> parameters = new List<LLVMValueRef>();
            for (int i = 0; i < funcDecl.Parameters.Count; ++i)
            {
                var p = funcDecl.Parameters[i];
                if (p.ParameterModificator == ParameterModificator.Arglist)
                {
                    // need to create va_list and va_start
                    apAlloca = _builder.BuildAlloca(HapetTypeToLLVMType(vaListType), $"va_list.ap.addr");
                    apAllocaBitcasted = _builder.BuildBitCast(apAlloca, _context.Int8Type.GetPointerTo(), "va_list.bitcasted");

                    // va start
                    var startFunc = GetVaStartFunc();
                    _builder.BuildCall2(startFunc.Item1, startFunc.Item2, new LLVMValueRef[] { apAllocaBitcasted });

                    var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(ptrToVaListType), apAllocaBitcasted, "va_list.ap.loaded");
                    parameters.Add(loaded);
                }
                else
                {
                    var vptr = _valueMap[p.Symbol];
                    var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(p.Type.OutType), vptr, p.Name.Name);
                    parameters.Add(loaded);
                }
            }

            // if there is smth to return
            if (funcDecl.Returns.OutType is VoidType)
            {
                _builder.BuildCall2(funcType, funcValue, parameters.ToArray());
                TryBuildVaEnd();
                _builder.BuildRetVoid();
            }
            else
            {
                var v = _builder.BuildCall2(funcType, funcValue, parameters.ToArray(), $"{entryPoint}Result");
                TryBuildVaEnd();
                _builder.BuildRet(v);
            }            

            void TryBuildVaEnd()
            {
                // check for va
                if (funcDecl.Parameters.Count == 0 || funcDecl.Parameters.Last().ParameterModificator != ParameterModificator.Arglist)
                    return;

                // va end
                var endFunc = GetVaStartFunc();
                _builder.BuildCall2(endFunc.Item1, endFunc.Item2, new LLVMValueRef[] { apAllocaBitcasted });
            }
        }
    }
}
