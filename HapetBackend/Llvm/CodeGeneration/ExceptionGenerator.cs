using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System.Xml.Linq;
using System;
using System.Reflection;
using System.IO;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private void GenerateThrowStmt(AstThrowStmt stmt, bool isRethrow = false)
        {
            DeclSymbol helper;
            DeclSymbol methSymbol;
            LLVMValueRef methFunc;
            LLVMTypeRef methType;
            helper = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("ExceptionHelper"));

            // if not rethrow - generate the exception. if rethrow - exception already exists
            if (!isRethrow)
            {
                // generating exception
                var exc = GenerateExpressionCode(stmt.ThrowExpression);
                // and store it in ExceptionHelper
                // WARN: hard cock
                methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("SetException")) as DeclSymbol;
                methFunc = _valueMap[methSymbol];
                methType = _typeMap[methSymbol.Decl.Type.OutType];
                _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { exc });

                var stackTraceClass = _currentFunction.Scope.GetSymbolInNamespace("System", new AstIdExpr("StackTrace"));
                methSymbol = (stackTraceClass.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("CopyStackTrace")) as DeclSymbol;
                methFunc = _valueMap[methSymbol];
                methType = _typeMap[methSymbol.Decl.Type.OutType];
                var copiedStackTrace = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { });
                methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("SetExceptionStackTrace")) as DeclSymbol;
                methFunc = _valueMap[methSymbol];
                methType = _typeMap[methSymbol.Decl.Type.OutType];
                _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { copiedStackTrace });
            }

            // getting last jmpbuf
            // WARN: hard cock
            methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Peek")) as DeclSymbol;
            methFunc = _valueMap[methSymbol];
            methType = _typeMap[methSymbol.Decl.Type.OutType];
            var jmpBuf = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] {  }, "jmpbuf");

            // making longjmp
            methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("LongJmp")) as DeclSymbol;
            methFunc = _valueMap[methSymbol];
            methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { jmpBuf, LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)1) });
            // create unreachable
            _builder.BuildUnreachable();
        }

        private List<AstTryCatchStmt> _tryCatchStatements = new List<AstTryCatchStmt>();
        private List<LLVMValueRef> _needGoBackVariables = new List<LLVMValueRef>();
        private List<LLVMBasicBlockRef> _finallyBlocks = new List<LLVMBasicBlockRef>();
        private List<List<LLVMBasicBlockRef>> _indirectBlockBlocks = new List<List<LLVMBasicBlockRef>>();
        private unsafe void GenerateTryCatchStmt(AstTryCatchStmt stmt)
        {
            // add current one
            _tryCatchStatements.Add(stmt);

            DeclSymbol helper;
            DeclSymbol methSymbol;
            LLVMValueRef methFunc;
            LLVMTypeRef methType;
            helper = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("ExceptionHelper"));

            // this var will contain 1 if the jmpBuf is already popped in dispatch block
            LLVMValueRef isJmpBufferPopped = CreateLocalVariable(HapetType.CurrentTypeContext.BoolTypeInstance, "isJmpBufferPopped");

            // this variable will contain 'ptr' if finally has to go back using 'indirectbr'
            LLVMValueRef needGoBack = CreateLocalVariable(HapetType.CurrentTypeContext.PtrToVoidType, "needGoBack");
            var nullValue = LLVMValueRef.CreateConstPointerNull(HapetTypeToLLVMType(HapetType.CurrentTypeContext.PtrToVoidType));
            _builder.BuildStore(nullValue, needGoBack);
            _needGoBackVariables.Add(needGoBack);
            _indirectBlockBlocks.Add(new List<LLVMBasicBlockRef>());

            var jmpBuf = CreateJmpBuffer();
            PushJmpBuf(jmpBuf);
            var setJmpResult = CreateSetJmpCall(jmpBuf);

            // creating required blocks
            var bbTry = _context.CreateBasicBlock($"try.block");
            var bbDispatch = _context.CreateBasicBlock($"dispatch.main.block");
            var bbFinally = _context.CreateBasicBlock($"finally.block");
            var bbRethrow = _context.CreateBasicBlock($"rethrow.block");
            var bbEnd = _context.CreateBasicBlock($"try.catch.end");

            // pushing finally block
            _finallyBlocks.Add(bbFinally);

            // compare to 1
            var binOp = SearchBinOp("==", HapetType.CurrentTypeContext.GetIntType(4, true), HapetType.CurrentTypeContext.GetIntType(4, true));
            var resCmp = binOp(_builder, setJmpResult, LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)1), "cmpResult");
            _builder.BuildCondBr(resCmp, bbDispatch, bbTry); // if 0 - try, if 1 - dispatch

            // first - try
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbTry);
            _builder.PositionAtEnd(bbTry);
            // generating try block code
            GenerateExpressionCode(stmt.TryBlock);
            // check if it has br/ret 
            if (!AstBlockExpr.IsBlockHasItsOwnBr(stmt.TryBlock))
            {
                // setting br into the block
                _builder.BuildBr(bbFinally);
            }

            // create catch dispatch/blocks
            // -1 because we already have main dispatch block
            var disps = new LLVMBasicBlockRef[stmt.CatchBlocks.Count == 0 ? 0 : stmt.CatchBlocks.Count - 1];
            var catches = new LLVMBasicBlockRef[stmt.CatchBlocks.Count];
            for (int i = 0; i < stmt.CatchBlocks.Count; ++i)
            {
                if (i + 1 != stmt.CatchBlocks.Count)
                    disps[i] = _context.CreateBasicBlock($"dispatch{i}.block");
                catches[i] = _context.CreateBasicBlock($"catch{i}.block");
            }

            // second - dispatch and catch blocks
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbDispatch);
            _builder.PositionAtEnd(bbDispatch);
            PopJmpBuf();
            _builder.BuildStore(HapetValueToLLVMValue(HapetType.CurrentTypeContext.BoolTypeInstance, true), isJmpBufferPopped);
            // if there are no breaks - just go to finally
            if (stmt.CatchBlocks.Count == 0)
            {
                _builder.BuildBr(bbRethrow);
            }
            // else go dispatch
            else
            {
                // getting current exception
                // WARN: hard cock
                methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("GetException")) as DeclSymbol;
                methFunc = _valueMap[methSymbol];
                methType = _typeMap[methSymbol.Decl.Type.OutType];
                var currException = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { }, "currExc");

                // generate all catch moves and blocks
                var currCatchIndex = 0;
                while (currCatchIndex < stmt.CatchBlocks.Count)
                {
                    var nextCatch = catches[currCatchIndex];

                    // check if could be casted
                    var excClass = _currentFunction.Scope.GetSymbolInNamespace("System", new AstIdExpr("Exception"));
                    LLVMValueRef canBeCasted = CheckIsCouldBeCasted(
                        currException, 
                        PointerType.GetPointerType(excClass.Decl.Type.OutType), 
                        stmt.CatchBlocks[currCatchIndex].CatchParam.Type.OutType, false, 
                        stmt.CatchBlocks[currCatchIndex].CatchParam.Location);

                    // if not last catch - build br to next catch
                    if (currCatchIndex + 1 != stmt.CatchBlocks.Count)
                    {
                        // getting the next dispatch
                        var nextDispatch = disps[currCatchIndex];
                        // if 0 - go dispatch again, if 1 - go catch
                        _builder.BuildCondBr(canBeCasted, nextCatch, nextDispatch);

                        // append the dispatch
                        LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, nextDispatch);
                        _builder.PositionAtEnd(nextDispatch);
                    }
                    // if last - build br to finally
                    else 
                    {
                        // if 0 - go rethrow, if 1 - go catch
                        _builder.BuildCondBr(canBeCasted, nextCatch, bbRethrow);
                    }
                    currCatchIndex++;
                }

                // making all the catches
                for (int i = 0; i < stmt.CatchBlocks.Count; ++i) 
                {
                    // append the catch
                    var currRealCatch = stmt.CatchBlocks[i];
                    var currCatch = catches[i];
                    LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, currCatch);
                    _builder.PositionAtEnd(currCatch);

                    if (currRealCatch.CatchParam.Name != null)
                    {
                        // creating exception variable to handle cought exception
                        var excVar = CreateLocalVariable(currRealCatch.CatchParam.Type.OutType, currRealCatch.CatchParam.Name.Name);
                        AssignToVar(excVar, currException);
                        _valueMap[currRealCatch.CatchParam.Symbol] = excVar;
                    }
                    
                    // generate the catch block itself
                    GenerateExpressionCode(currRealCatch.CatchBlock);

                    // check if it has br/ret 
                    if (!AstBlockExpr.IsBlockHasItsOwnBr(currRealCatch.CatchBlock))
                    {
                        // setting br into the block
                        _builder.BuildBr(bbFinally);
                    }
                }
            }

            // third - rethrow
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbRethrow);
            _builder.PositionAtEnd(bbRethrow);
            // call finally before rethrow
            {
                // make the block into which execution will be returned
                var beforeRethrowBlock = _context.CreateBasicBlock($"before.rethrow");

                // set var that finally need to go back
                _builder.BuildStore(_lastFunctionValueRef.GetBlockAddress(beforeRethrowBlock), needGoBack);
                // increase amount of go backs
                _indirectBlockBlocks[_indirectBlockBlocks.Count - 1].Add(beforeRethrowBlock);
                // and build br to the finally
                _builder.BuildBr(_finallyBlocks[_finallyBlocks.Count - 1]);

                // just make the block
                LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, beforeRethrowBlock);
                _builder.PositionAtEnd(beforeRethrowBlock);

                // making rethrow
                GenerateThrowStmt(null, true);
            }

            // fourth - finally
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbFinally);
            _builder.PositionAtEnd(bbFinally);

            // pop jmp buf if not popped
            var bbNeedToPop = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"pop.buffer");
            var bbPopped = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"finally.continue");
            var isJmpBufferPoppedLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(HapetType.CurrentTypeContext.BoolTypeInstance), isJmpBufferPopped, "isJmpBufferPoppedLoaded");
            _builder.BuildCondBr(isJmpBufferPoppedLoaded, bbPopped, bbNeedToPop); // if 0 - pop, if 1 - continue
            _builder.PositionAtEnd(bbNeedToPop);
            PopJmpBuf();
            _builder.BuildBr(bbPopped);
            _builder.PositionAtEnd(bbPopped);

            // if user defined block exists - generate statements from it
            if (stmt.FinallyBlock != null)
            {
                GenerateExpressionCode(stmt.FinallyBlock);
            }

            // cringe shite to always handle finally block before exits
            var needGoBackLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(HapetType.CurrentTypeContext.IntPtrTypeInstance), needGoBack);
            var zeroNullValue = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.IntPtrTypeInstance), 0);
            var isNullCmp = _builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, needGoBackLoaded, zeroNullValue);

            var bbNeedGoBack = _context.CreateBasicBlock($"finally.goback.block");
            var bbNoNeedGoBack = _context.CreateBasicBlock($"finally.nogoback.block");
            // if 0 - no go back, if ptr - go back
            _builder.BuildCondBr(isNullCmp, bbNoNeedGoBack, bbNeedGoBack);
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbNeedGoBack);
            _builder.PositionAtEnd(bbNeedGoBack);
            // make indirectbr here
            var needGoBackLoadedAsPtr = _builder.BuildLoad2(HapetTypeToLLVMType(HapetType.CurrentTypeContext.PtrToVoidType), needGoBack);
            var indirectBr = _builder.BuildIndirectBr(needGoBackLoadedAsPtr, (uint)_indirectBlockBlocks[_indirectBlockBlocks.Count - 1].Count);
            foreach (var bl in _indirectBlockBlocks[_indirectBlockBlocks.Count - 1])
                indirectBr.AddDestination(bl);
            // make normal block
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbNoNeedGoBack);
            _builder.PositionAtEnd(bbNoNeedGoBack);
            // check if it has br/ret 
            if (!AstBlockExpr.IsBlockHasItsOwnBr(stmt.FinallyBlock))
            {
                // setting br into the block
                _builder.BuildBr(bbEnd);
            }

            // append the end
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);
            _builder.PositionAtEnd(bbEnd);

            // popping it
            _finallyBlocks.RemoveAt(_finallyBlocks.Count - 1);
            _needGoBackVariables.RemoveAt(_needGoBackVariables.Count - 1);
            _indirectBlockBlocks.RemoveAt(_indirectBlockBlocks.Count - 1);
            _tryCatchStatements.RemoveAt(_tryCatchStatements.Count - 1);
        }

        private LLVMValueRef CreateJmpBuffer()
        {
            // alloca jmpbuf
            var jmpBufStruct = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("JmpBuf"));
            LLVMValueRef jmpBuf;
            // for windows x64 target we need to manually align it up to 16
            if (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform == HapetFrontend.TargetPlatform.Win64)
                jmpBuf = CreateLocalVariable(HapetTypeToLLVMType(jmpBufStruct.Decl.Type.OutType), 16, "jmpbuf");
            else
                jmpBuf = CreateLocalVariable(jmpBufStruct.Decl.Type.OutType, "jmpbuf");
            return jmpBuf;
        }

        private void PushJmpBuf(LLVMValueRef value)
        {
            // pushing current jmpbuf 
            // WARN: hard cock
            var helper = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("ExceptionHelper"));
            var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Push")) as DeclSymbol;
            var methFunc = _valueMap[methSymbol];
            var methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { value });
        }

        private LLVMValueRef CreateSetJmpCall(LLVMValueRef jmpBuf)
        {
            LLVMValueRef setJmpResult;
            var helper = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("ExceptionHelper"));

            // on Windows x64 platform 'setjmp' function receives 2 parameters and should call FrameAddress
            if (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform == HapetFrontend.TargetPlatform.Win64)
            {
                // call frameaddress
                var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("FrameAddress")) as DeclSymbol;
                var methFunc = _valueMap[methSymbol];
                var methType = _typeMap[methSymbol.Decl.Type.OutType];
                var addrPtr = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)0) });
                // call setjmp
                methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("SetJmp")) as DeclSymbol;
                methFunc = _valueMap[methSymbol];
                methType = _typeMap[methSymbol.Decl.Type.OutType];
                setJmpResult = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { jmpBuf, addrPtr });
            }
            // on Windows x86 platform 'setjmp' function receives 2 parameters and arglist
            else if (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform == HapetFrontend.TargetPlatform.Win86)
            {
                // call setjmp
                var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("SetJmp")) as DeclSymbol;
                var methFunc = _valueMap[methSymbol];
                var methType = _typeMap[methSymbol.Decl.Type.OutType];
                setJmpResult = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { jmpBuf, LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)0) });
            }
            else
            {
                // call setjmp
                var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("SetJmp")) as DeclSymbol;
                var methFunc = _valueMap[methSymbol];
                var methType = _typeMap[methSymbol.Decl.Type.OutType];
                setJmpResult = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { jmpBuf });
            }
            return setJmpResult;
        }

        private void PopJmpBuf()
        {
            // popping current jmpbuf 
            // WARN: hard cock
            var helper = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("ExceptionHelper"));
            var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Pop")) as DeclSymbol;
            var methFunc = _valueMap[methSymbol];
            var methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { });
        }

        private void PushStackTrace(string name)
        {
            LLVMValueRef funcNameConst = HapetValueToLLVMValue(HapetType.CurrentTypeContext.StringTypeInstance, name);
            // push stacktrace
            var helper = _currentFunction.Scope.GetSymbolInNamespace("System", new AstIdExpr("StackTrace"));
            var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Push")) as DeclSymbol;
            var methFunc = _valueMap[methSymbol];
            var methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { funcNameConst });
        }

        private void PopStackTrace()
        {
            // push stacktrace
            var helper = _currentFunction.Scope.GetSymbolInNamespace("System", new AstIdExpr("StackTrace"));
            var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Pop")) as DeclSymbol;
            var methFunc = _valueMap[methSymbol];
            var methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { });
        }

        private void GenerateNullReferenceException(string message)
        {
            var cls = _currentFunction.Scope.GetSymbolInNamespace("System", new AstIdExpr("NullReferenceException"));
            var methSymbol = (cls.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Throw")) as DeclSymbol;
            var methFunc = _valueMap[methSymbol];
            var methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { HapetValueToLLVMValue(HapetType.CurrentTypeContext.StringTypeInstance, message) });
        }
    }
}
