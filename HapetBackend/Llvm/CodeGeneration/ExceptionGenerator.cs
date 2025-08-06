using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System.Xml.Linq;
using System;
using System.Reflection;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private void GenerateThrowStmt(AstThrowStmt stmt)
        {
            DeclSymbol helper;
            DeclSymbol methSymbol;
            LLVMValueRef methFunc;
            LLVMTypeRef methType;

            // generating exception
            var exc = GenerateExpressionCode(stmt.ThrowExpression);
            // and store it in ExceptionHelper
            // WARN: hard cock
            helper = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("ExceptionHelper"));
            methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("SetException")) as DeclSymbol;
            methFunc = _valueMap[methSymbol];
            methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { exc });

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

        private unsafe void GenerateTryCatchStmt(AstTryCatchStmt stmt)
        {
            // this variable will be 'true' if finnaly has to go back using 'indirectbr'
            LLVMValueRef needGoBack = CreateLocalVariable(HapetType.CurrentTypeContext.BoolTypeInstance, "needGoBack");
            _builder.BuildStore(LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.BoolTypeInstance), 0), needGoBack);

            // alloca jmpbuf
            var jmpBufStruct = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("JmpBuf"));
            LLVMValueRef jmpBuf;
            // for windows x64 target we need to manually align it up to 16
            if (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform == HapetFrontend.TargetPlatform.Win64)
                jmpBuf = CreateLocalVariable(HapetTypeToLLVMType(jmpBufStruct.Decl.Type.OutType), 16, "jmpbuf");
            else
                jmpBuf = CreateLocalVariable(jmpBufStruct.Decl.Type.OutType, "jmpbuf");

            // pushing current jmpbuf 
            // WARN: hard cock
            var helper = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("ExceptionHelper"));
            var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Push")) as DeclSymbol;
            var methFunc = _valueMap[methSymbol];
            var methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { jmpBuf });

            LLVMValueRef setJmpResult;
            // on Windows x64 platform 'setjmp' function receives 2 parameters and should call FrameAddress
            if (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform == HapetFrontend.TargetPlatform.Win64)
            {
                // call frameaddress
                methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("FrameAddress")) as DeclSymbol;
                methFunc = _valueMap[methSymbol];
                methType = _typeMap[methSymbol.Decl.Type.OutType];
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
                methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("SetJmp")) as DeclSymbol;
                methFunc = _valueMap[methSymbol];
                methType = _typeMap[methSymbol.Decl.Type.OutType];
                setJmpResult = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { jmpBuf, LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)0) });
            }
            else
            {
                // call setjmp
                methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("SetJmp")) as DeclSymbol;
                methFunc = _valueMap[methSymbol];
                methType = _typeMap[methSymbol.Decl.Type.OutType];
                setJmpResult = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { jmpBuf });
            }

            // creating required blocks
            var bbTry = _context.CreateBasicBlock($"try.block");
            var bbDispatch = _context.CreateBasicBlock($"dispatch.main.block");
            var bbFinally = _context.CreateBasicBlock($"finally.block");
            var bbRethrow = _context.CreateBasicBlock($"rethrow.block");
            var bbEnd = _context.CreateBasicBlock($"try.catch.end");

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
            // if there are no breaks - just go to finally
            if (stmt.CatchBlocks.Count == 0)
            {
                _builder.BuildBr(bbFinally);
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
                        var nextDispatch = catches[currCatchIndex];
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
                    // creating exception variable to handle cought exception
                    var excVar = CreateLocalVariable(currRealCatch.CatchParam.Type.OutType, currRealCatch.CatchParam.Name.Name);
                    AssignToVar(excVar, currException);
                    _valueMap[currRealCatch.CatchParam.Symbol] = excVar;
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
            // TODO: call finally before rethrow and remove below call after it
            methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Pop")) as DeclSymbol;
            methFunc = _valueMap[methSymbol];
            methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { });
            // getting next jmpbuf
            methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Peek")) as DeclSymbol;
            methFunc = _valueMap[methSymbol];
            methType = _typeMap[methSymbol.Decl.Type.OutType];
            var jmpBufNext = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { }, "jmpbufNext");
            // making longjmp
            methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("LongJmp")) as DeclSymbol;
            methFunc = _valueMap[methSymbol];
            methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { jmpBufNext, LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)1) });
            // create unreachable
            _builder.BuildUnreachable();

            // fourth - finally
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbFinally);
            _builder.PositionAtEnd(bbFinally);
            // popping current jmpbuf 
            // WARN: hard cock
            methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Pop")) as DeclSymbol;
            methFunc = _valueMap[methSymbol];
            methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { });
            // if user defined block exists - generate statements from it
            if (stmt.FinallyBlock != null)
            {
                GenerateExpressionCode(stmt.FinallyBlock);
            }

            // cringe shite to always handle finally block before exits
            var needGoBackLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(HapetType.CurrentTypeContext.BoolTypeInstance), needGoBack);
            var bbNeedGoBack = _context.CreateBasicBlock($"finally.goback.block");
            var bbNoNeedGoBack = _context.CreateBasicBlock($"finally.nogoback.block");
            // if 0 - no go back, if 1 - go back
            _builder.BuildCondBr(needGoBackLoaded, bbNeedGoBack, bbNoNeedGoBack);
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbNeedGoBack);
            _builder.PositionAtEnd(bbNeedGoBack);
            // TODO: make indirectbr here
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
        }
    }
}
