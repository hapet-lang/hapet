using HapetFrontend.Ast.Statements;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        //private unsafe LLVMValueRef GenerateNormalStringParam(LLVMTypeRef[] paramTypes, LLVMValueRef mainFunc)
        //{
        //    // generating params allocs
        //    // there are 2 params: argc and argv
        //    var addrAllocaArgc = _builder.BuildAlloca(paramTypes[0], $"argc.addr");
        //    _builder.BuildStore(mainFunc.GetParam((uint)0), addrAllocaArgc);
        //    var argc = _builder.BuildLoad2(paramTypes[0], addrAllocaArgc, "argc");
        //    var addrAllocaArgv = _builder.BuildAlloca(paramTypes[1], $"argv.addr");
        //    _builder.BuildStore(mainFunc.GetParam((uint)1), addrAllocaArgv);
        //    var argv = _builder.BuildLoad2(paramTypes[1], addrAllocaArgv, "argv");

        //    #region For loop
        //    // creating an 'i' var to calc loops
        //    var ivarPtr = CreateLocalVariable(IntType.DefaultType, "i");
        //    var zeroToI = LLVM.ConstInt(HapetTypeToLLVMType(IntType.DefaultType), ((NumberData)0).ToULong(), 1);
        //    _builder.BuildStore(zeroToI, ivarPtr);

        //    // build a 'for' to loop over the args
        //    var bbCond = _lastFunctionValueRef.AppendBasicBlock($"for_args.cond");
        //    var bbBody = _lastFunctionValueRef.AppendBasicBlock($"for_args.body");

        //    // creating other blocks
        //    var bbInc = _context.CreateBasicBlock($"for_args.inc");
        //    var bbEnd = _context.CreateBasicBlock($"for_args.end");

        //    // directly br into loop condition
        //    _builder.BuildBr(bbCond);

        //    // condition
        //    _builder.PositionAtEnd(bbCond);
        //    // building the condition
        //    var left = _builder.BuildLoad2(HapetTypeToLLVMType(IntType.DefaultType), ivarPtr, "i.loaded");
        //    var right = argc; // already loaded, so direct use
        //    var bo = builtInBinOperators[("<", IntType.DefaultType, IntType.DefaultType)];
        //    var cmp = bo(_builder, left, right, "binOp");
        //    _builder.BuildCondBr(cmp, bbBody, bbEnd);

        //    // body
        //    _builder.PositionAtEnd(bbBody);
        //    if (stmt.Body != null)
        //    {
        //        // generating body code
        //        GenerateExpressionCode(stmt.Body);
        //    }

        //    // appending them sooner
        //    LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbInc);
        //    LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);

        //    // setting br without condition into inc block from body block
        //    _builder.BuildBr(bbInc);

        //    // inc
        //    _builder.PositionAtEnd(bbInc);
        //    var oldI = _builder.BuildLoad2(HapetTypeToLLVMType(IntType.DefaultType), ivarPtr, "i.loaded");
        //    var oneConst = LLVM.ConstInt(HapetTypeToLLVMType(IntType.DefaultType), ((NumberData)1).ToULong(), 1);
        //    var bo2 = builtInBinOperators[("+", IntType.DefaultType, IntType.DefaultType)];
        //    var summ = bo2(_builder, oldI, oneConst, "summ");
        //    _builder.BuildStore(summ, ivarPtr);

        //    _builder.BuildBr(bbCond);
        //    _builder.PositionAtEnd(bbEnd);
        //    #endregion
        //}
    }
}
