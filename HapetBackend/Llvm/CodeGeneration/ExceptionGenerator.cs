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
            // generating exception
            var exc = GenerateExpressionCode(stmt.ThrowExpression);
            // and store it in ExceptionHelper
            // WARN: hard cock
            var helper = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("ExceptionHelper"));
            var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("SetException")) as DeclSymbol;
            var methFunc = _valueMap[methSymbol];
            LLVMTypeRef methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { exc });

            // getting last jmpbuf
            // WARN: hard cock
            methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Peek")) as DeclSymbol;
            methFunc = _valueMap[methSymbol];
            methType = _typeMap[methSymbol.Decl.Type.OutType];
            var jmpBuf = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] {  }, "jmpBuf");

            // making longjmp
            // WARN: hard cock
            methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("LongJmp")) as DeclSymbol;
            methFunc = _valueMap[methSymbol];
            methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { jmpBuf, LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)1) });
        }

        private unsafe void GenerateTryCatchStmt(AstTryCatchStmt stmt)
        {
            // alloca jmpbuf
            var jmpBufStruct = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("JmpBuf"));
            var varPtr = CreateLocalVariable(jmpBufStruct.Decl.Type.OutType, "jmpbuf");

            // pushing current jmpbuf 
            // WARN: hard cock
            var helper = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("ExceptionHelper"));
            var methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Push")) as DeclSymbol;
            var methFunc = _valueMap[methSymbol];
            var methType = _typeMap[methSymbol.Decl.Type.OutType];
            _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { varPtr });

            // call setjmp
            // WARN: hard cock
            methSymbol = (helper.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("SetJmp")) as DeclSymbol;
            methFunc = _valueMap[methSymbol];
            methType = _typeMap[methSymbol.Decl.Type.OutType];
            var res = _builder.BuildCall2(methType, methFunc, new LLVMValueRef[] { varPtr });

            // creating required blocks
            var bbTry = _context.CreateBasicBlock($"try.block");
            var bbDispatch = _context.CreateBasicBlock($"dispatch.main.block");
            var bbFinally = _context.CreateBasicBlock($"finally.block");

            // compare to 1
            var binOp = SearchBinOp("==", HapetType.CurrentTypeContext.GetIntType(4, true), HapetType.CurrentTypeContext.GetIntType(4, true));
            var resCmp = binOp(_builder, res, LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)1), "cmpResult");
            _builder.BuildCondBr(resCmp, bbDispatch, bbTry); // if 0 - try, if 1 - dispatch

            // first - try
            LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbTry);
            _builder.PositionAtEnd(bbTry);
            // generating try block code
            GenerateExpressionCode(stmt.TryBlock);

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

            // second - main dispatch
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
                    // check if could be casted
                    LLVMValueRef cmpRes = default;
                    var nextCatch = catches[currCatchIndex];

                    // if not last catch - build br to next catch
                    if (currCatchIndex + 1 != stmt.CatchBlocks.Count)
                    {
                        // getting the next dispatch
                        var nextDispatch = catches[currCatchIndex];
                        // if 0 - go dispatch again, if 1 - go catch
                        _builder.BuildCondBr(cmpRes, nextDispatch, nextCatch);

                        // append the dispatch
                        LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, nextDispatch);
                        _builder.PositionAtEnd(nextDispatch);
                    }
                    // if last - build br to finally
                    else 
                    {
                        // if 0 - go finally, if 1 - go catch
                        _builder.BuildCondBr(cmpRes, bbFinally, nextCatch);
                    }
                    currCatchIndex++;
                }

                // making all the catches
                for (int i = 0; i < stmt.CatchBlocks.Count; ++i) 
                {
                    
                }
            }
        }
    }
}
