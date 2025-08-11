using HapetFrontend;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System.Data.Common;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private unsafe void GenerateMainFunction()
        {
            AstFuncDecl pseudoMainFunc = new AstFuncDecl(null, null, null, new AstIdExpr("__main"))
            {
                Scope = _compiler.MainFunction.Scope,
            };
            string mainFuncName = null;
            LLVMTypeRef returnType = _context.VoidType;
            LLVMTypeRef[] paramTypes = Array.Empty<LLVMTypeRef>();

            LLVMTypeRef crtMainFuncType = null;
            LLVMValueRef crtMainFunc = null;

            switch (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform)
            {
                case TargetPlatform.Win86:
                case TargetPlatform.Win64:
                    // creating crt main func extern
                    crtMainFuncType = LLVMTypeRef.CreateFunction(_context.VoidType, Array.Empty<LLVMTypeRef>(), false);
                    crtMainFunc = _module.AddFunction("__main", crtMainFuncType);
                    crtMainFunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
                    var crtEntry = crtMainFunc.AppendBasicBlockInContext(_context, "entry");
                    _builder.PositionAtEnd(crtEntry);
                    _builder.BuildRetVoid();

                    mainFuncName = "main";
                    returnType = _context.Int32Type;
                    // i32 %argc, ptr %argv
                    paramTypes = new LLVMTypeRef[] { _context.Int32Type, _voidPointerType };
                    break;
                case TargetPlatform.Linux86:
                    mainFuncName = "main";
                    returnType = _context.Int32Type;
                    break;
                case TargetPlatform.Linux64:
                    mainFuncName = "main";
                    returnType = _context.Int32Type;
                    break;
            }

            var ltype = LLVMTypeRef.CreateFunction(returnType, paramTypes, false);
            var lfunc = _module.AddFunction(mainFuncName, ltype);
            lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;

            // calling conventions
            // https://github.com/llvm/llvm-project/blob/main/llvm/include/llvm/IR/CallingConv.h
            switch (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform)
            {
                case TargetPlatform.Win86:
                    //if (CompilerSettings.TargetRepresentation == TargetRepresentation.Windowed)
                    //	lfunc.FunctionCallConv = (uint)LLVMCallConv.LLVMX86StdcallCallConv; // X86_StdCall
                    break;
                case TargetPlatform.Win64:
                    // no need for this because it is defaulted by LLVM itself
                    // lfunc.FunctionCallConv = 79; // Win64
                    break;
                case TargetPlatform.Linux86:
                    break;
                case TargetPlatform.Linux64:
                    break;
            }
            _currentFunction = pseudoMainFunc;

            var entry = lfunc.AppendBasicBlockInContext(_context, "entry");
            var pars = lfunc.AppendBasicBlockInContext(_context, "params");
            var main = lfunc.AppendBasicBlockInContext(_context, "mainpart");

            _builder.PositionAtEnd(entry);
            // if crt main func call should be placed 
            if (crtMainFunc != null)
            {
                _builder.BuildCall2(crtMainFuncType, crtMainFunc, Array.Empty<LLVMValueRef>());
            }
            _builder.BuildBr(pars);

            _builder.PositionAtEnd(pars);

            // generating params allocs
            List<LLVMValueRef> mainFuncParams = new List<LLVMValueRef>();
            for (int i = 0; i < paramTypes.Length; ++i)
            {
                var pType = paramTypes[i];
                var addrAlloca = _builder.BuildAlloca(pType, $"p{i}.addr");
                _builder.BuildStore(lfunc.GetParam((uint)i), addrAlloca);
                mainFuncParams.Add(_builder.BuildLoad2(pType, addrAlloca));
            }

            _builder.BuildBr(main);

            _builder.PositionAtEnd(main);

            DeclSymbol parentDecl;
            DeclSymbol funcDecl;
            LLVMValueRef funcValue;
            LLVMTypeRef funcType;
            // some special calls before main code begins
            {
                // need to initialize typeinfos and vtables before any code
                _builder.BuildCall2(_typeInfoInitializer.Item1, _typeInfoInitializer.Item2, []);
                _builder.BuildCall2(_vTableInitializer.Item1, _vTableInitializer.Item2, []);

                // need to call at first stors of System.StackTrace and System.Runtime.InteropServices.ExceptionHelper
                parentDecl = _compiler.MainFunction.Scope.GetSymbolInNamespace("System", new AstIdExpr("StackTrace"));
                funcDecl = (parentDecl.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("StackTrace_stor")) as DeclSymbol;
                funcValue = _valueMap[funcDecl];
                funcType = _typeMap[funcDecl.Decl.Type.OutType];
                _builder.BuildCall2(funcType, funcValue, []);
                parentDecl = _compiler.MainFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("ExceptionHelper"));
                funcDecl = (parentDecl.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("ExceptionHelper_stor")) as DeclSymbol;
                funcValue = _valueMap[funcDecl];
                funcType = _typeMap[funcDecl.Decl.Type.OutType];
                _builder.BuildCall2(funcType, funcValue, []);

                // making global try-catch
                // only try and catch here
                var bbTry = _context.CreateBasicBlock($"try.block");
                var bbCatch = _context.CreateBasicBlock($"catch.block");
                {
                    // add current one
                    _tryCatchStatements.Add(new AstTryCatchStmt(null, null, null));

                    /// WARN: the same as in <see cref="GenerateTryCatchStmt"/>
                    LLVMValueRef needGoBack = CreateLocalVariable(HapetType.CurrentTypeContext.PtrToVoidType, "needGoBack");
                    var nullValue = LLVMValueRef.CreateConstPointerNull(HapetTypeToLLVMType(HapetType.CurrentTypeContext.PtrToVoidType));
                    _builder.BuildStore(nullValue, needGoBack);
                    _needGoBackVariables.Add(needGoBack);
                    _indirectBlockBlocks.Add(new List<LLVMBasicBlockRef>());

                    var jmpBuf = CreateJmpBuffer();
                    PushJmpBuf(jmpBuf);
                    var setJmpResult = CreateSetJmpCall(jmpBuf);

                    // pushing "finally" block :))
                    _finallyBlocks.Add(bbCatch);

                    // compare to 1
                    var binOp = SearchBinOp("==", HapetType.CurrentTypeContext.GetIntType(4, true), HapetType.CurrentTypeContext.GetIntType(4, true));
                    var resCmp = binOp(_builder, setJmpResult, LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)1), "cmpResult");
                    _builder.BuildCondBr(resCmp, bbCatch, bbTry); // if 0 - try, if 1 - catch

                    LLVM.AppendExistingBasicBlock(lfunc, bbCatch);
                    _builder.PositionAtEnd(bbCatch);
                    // TODO: get exception and print stacktrace
                    parentDecl = _compiler.MainFunction.Scope.GetSymbolInNamespace("System", new AstIdExpr("Console"));
                    funcDecl = (parentDecl.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("WriteLine")) as DeclSymbol;
                    funcValue = _valueMap[funcDecl];
                    funcType = _typeMap[funcDecl.Decl.Type.OutType];
                    _builder.BuildCall2(funcType, funcValue, new LLVMValueRef[] { HapetValueToLLVMValue(HapetType.CurrentTypeContext.StringTypeInstance, "Exception...") });
                    _builder.BuildRet(LLVMValueRef.CreateConstInt(returnType, 1));

                    LLVM.AppendExistingBasicBlock(lfunc, bbTry);
                    _builder.PositionAtEnd(bbTry);
                }

                // need to call stors caller
                GenerateFuncCode(_compiler.StorsCallerFunction, null, true);
                GenerateFuncCode(_compiler.StorsCallerFunction, null, false);
                _currentFunction = pseudoMainFunc;
                _builder.PositionAtEnd(bbTry);
                var callerValue = _valueMap[_compiler.StorsCallerFunction.Symbol];
                var callerType = _typeMap[_compiler.StorsCallerFunction.Type.OutType];
                _builder.BuildCall2(callerType, callerValue, []);
            }

            // calling proper function to create normal string array from this shite of C
            parentDecl = _compiler.MainFunction.Scope.GetSymbolInNamespace("System.Text", new AstIdExpr("Native"));
            funcDecl = (parentDecl.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("GetParametersArray")) as DeclSymbol;
            funcValue = _valueMap[funcDecl];
            funcType = _typeMap[funcDecl.Decl.Type.OutType];
            LLVMValueRef stringArray = _builder.BuildCall2(funcType, funcValue, mainFuncParams.ToArray(), "stringArr");
            var parsss = new LLVMValueRef[] { stringArray };
            //var parsss = new LLVMValueRef[] { LLVM.ConstPointerNull(HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetArrayType(HapetType.CurrentTypeContext.StringTypeInstance))) };

            { // call main function
                funcValue = _valueMap[_compiler.MainFunction.Symbol];
                funcType = _typeMap[_compiler.MainFunction.Type.OutType];
                var exitCode = _builder.BuildCall2(funcType, funcValue, parsss, "exitCode");
                _builder.BuildRet(exitCode);
            }
        }
    }
}
