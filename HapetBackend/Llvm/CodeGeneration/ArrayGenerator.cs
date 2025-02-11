using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        /// <summary>
        /// Generates fully clear array with default values
        /// </summary>
        /// <param name="expr">The array expr</param>
        /// <returns>Array struct</returns>
        private unsafe LLVMValueRef GenerateArrayInternal(AstArrayCreateExpr expr, bool getPtr = false)
        {
            ArrayType arrType = ArrayType.GetArrayType(expr.TypeName.OutType, expr.Scope);
            AstExpression currentSizeExpr = expr.SizeExprs.Last();
            LLVMValueRef currentArraySizeValueRef = GenerateExpressionCode(currentSizeExpr);
            LLVMTypeRef arrayTypeRef = HapetTypeToLLVMType(arrType);

            // allocating mem for the array buf
            LLVMValueRef allocated;
            if (expr.SizeExprs.Count > 1)
                // allocating memory for the array
                allocated = GetMalloc(arrType.GetSize(), currentArraySizeValueRef);
            else
                // allocating memory for the data in array
                allocated = GetMalloc(expr.TypeName.OutType.GetSize(), currentArraySizeValueRef);

            // different generation depending on ini elements
            if (expr.Elements.Count > 0)
            {
                // do not generate loop (just craete it here) if there are ini elements!!!
                for (int i = 0; i < expr.Elements.Count; ++i)
                {
                    var iValueRef = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.GetIntType(4, true)), (ulong)i);
                    if (expr.SizeExprs.Count > 1)
                    {
                        // generate nested array with ini values
                        var arrayVal = GenerateArrayInternal(expr.Elements[i] as AstArrayCreateExpr);
                        var arrayBufEl = _builder.BuildGEP2(arrayTypeRef, allocated, new LLVMValueRef[] { iValueRef }, $"elementPtr{i}");
                        _builder.BuildStore(arrayVal, arrayBufEl);
                    }
                    else
                    {
                        // just normal values 
                        var theVal = GenerateExpressionCode(expr.Elements[i]);
                        var arrayBufEl = _builder.BuildGEP2(HapetTypeToLLVMType(expr.TypeName.OutType), allocated, new LLVMValueRef[] { iValueRef }, $"elementPtr{i}");
                        _builder.BuildStore(theVal, arrayBufEl);
                    }
                }
            }
            else
            {
                // WARN: this is just a stolen code from 'for' loop generation :)))
                _forCounter++;

                var varPtrI = CreateLocalVariable(IntType.DefaultType, "iVar");
                var zeroConst = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.DefaultType), (ulong)0);
                _builder.BuildStore(zeroConst, varPtrI);

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

                // building the condition
                var left = _builder.BuildLoad2(HapetTypeToLLVMType(IntType.DefaultType), varPtrI, "iLoaded");
                var bo = builtInBinOperators[("<", IntType.DefaultType, IntType.DefaultType)];
                var cmp = bo(_builder, left, currentArraySizeValueRef, "cmpOp");
                _builder.BuildCondBr(cmp, bbBody, bbEnd);

                // body
                _builder.PositionAtEnd(bbBody);
                // generating body code
                var iLoadedForBody = _builder.BuildLoad2(HapetTypeToLLVMType(IntType.DefaultType), varPtrI, "iLoadedBody");
                if (expr.SizeExprs.Count > 1)
                {
                    // generate nested array with no ini values
                    // removing the last array size
                    expr.SizeExprs.RemoveAt(expr.SizeExprs.Count - 1);
                    var arrayVal = GenerateArrayInternal(expr);
                    var arrayBufEl = _builder.BuildGEP2(arrayTypeRef, allocated, new LLVMValueRef[] { iLoadedForBody }, $"elementPtr");
                    _builder.BuildStore(arrayVal, arrayBufEl);
                }
                else
                {
                    // just normal default values 
                    var defaultVal = GenerateExpressionCode(AstDefaultExpr.GetDefaultValueForType(expr.TypeName.OutType, expr.TypeName));
                    var arrayBufEl = _builder.BuildGEP2(HapetTypeToLLVMType(expr.TypeName.OutType), allocated, new LLVMValueRef[] { iLoadedForBody }, $"elementPtr");
                    _builder.BuildStore(defaultVal, arrayBufEl);
                }

                // appending them sooner
                LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbInc);
                LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);

                // setting br without condition into inc block from body block
                _builder.BuildBr(bbInc);

                // inc
                _builder.PositionAtEnd(bbInc);

                // generating inc code
                var iLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(IntType.DefaultType), varPtrI, "iLoadedAgain");
                var oneConst = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.DefaultType), (ulong)1);
                var boSum = builtInBinOperators[("+", IntType.DefaultType, IntType.DefaultType)];
                var summ = boSum(_builder, iLoaded, oneConst, "summOp");
                _builder.BuildStore(summ, varPtrI);

                _builder.BuildBr(bbCond);
                _builder.PositionAtEnd(bbEnd);
            }

            // creating array variable
            var theArrayItself = CreateLocalVariable(arrType, "theArray");

            // the 1 is because ArrayType struct has buf field as it's 1 param
            var buf = _builder.BuildStructGEP2(arrayTypeRef, theArrayItself, 1, "arrayBuf");
            _builder.BuildStore(allocated, buf);
            /// setting the array size
            var len = _builder.BuildStructGEP2(arrayTypeRef, theArrayItself, 0, "arrayLen");
            _builder.BuildStore(currentArraySizeValueRef, len);

            if (getPtr)
                return theArrayItself;

            var theArrayItselfLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(arrType), theArrayItself, "theArrayLoaded");
            return theArrayItselfLoaded;
        }
    }
}
